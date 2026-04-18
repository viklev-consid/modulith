# How-to: Add Idempotency

Modulith deliberately does not ship idempotency infrastructure ([`../adr/0020-no-idempotency-infrastructure.md`](../adr/0020-no-idempotency-infrastructure.md)). When your use case requires it, this guide covers the extension patterns.

---

## When to add idempotency

You need it when:

- The API handles payments, provisioning, or any operation where duplicate execution causes real-world harm.
- Clients retry on network errors (most mobile/flaky-network clients do).
- Messages may be redelivered (Wolverine's outbox guarantees at-least-once).
- Background jobs may reschedule after a crash.

You don't need it when:

- Operations are naturally idempotent (`SetStatus(X)` is safe to repeat).
- Duplicate execution is harmless (recording an event more than once is fine for this domain).
- Your API is internal-only with well-behaved clients.

Default to natural idempotency in handler design; reach for infrastructure only when natural isn't enough.

---

## Three layers of idempotency

### 1. Natural idempotency (design discipline)

Prefer state-based operations over delta-based:

| Avoid | Prefer |
|---|---|
| `IncrementLoginCount()` | `RecordLogin(loginId, at)` with unique `loginId` |
| `AddBalance(100)` | `CreditAccount(paymentId, 100)` with unique `paymentId` |
| `AppendToHistory(entry)` | `RecordEntry(entryId, entry)` with unique `entryId` |

The pattern: operations carry a stable identifier. Repeating the operation with the same identifier produces the same result.

No infrastructure required. Pure design.

### 2. Handler-level deduplication (middleware + table)

For message handlers that aren't naturally idempotent:

**Table (per module):**

```sql
CREATE TABLE orders.processed_messages (
    message_id uuid PRIMARY KEY,
    handled_at timestamp with time zone NOT NULL DEFAULT now()
);
```

Migration:

```bash
dotnet ef migrations add AddProcessedMessages \
  --project src/Modules/Orders/Modulith.Modules.Orders \
  --context OrdersDbContext
```

**Middleware:**

```csharp
internal sealed class IdempotencyMiddleware
{
    public static async Task<HandlerContinuation> BeforeAsync(
        Envelope envelope,
        OrdersDbContext db,
        CancellationToken ct)
    {
        try
        {
            db.ProcessedMessages.Add(new ProcessedMessage
            {
                MessageId = envelope.Id,
                HandledAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync(ct);
            return HandlerContinuation.Continue;
        }
        catch (DbUpdateException) when (IsDuplicateKeyViolation(ex))
        {
            // Already processed — skip
            return HandlerContinuation.Stop;
        }
    }
}
```

**Registration:**

```csharp
builder.Host.UseWolverine(opts =>
{
    opts.Policies.AddMiddleware<IdempotencyMiddleware>(
        t => t.Namespace?.Contains("Orders.Integration") == true);
});
```

The `INSERT` happens inside Wolverine's automatic transaction. A duplicate fails the unique constraint and the middleware stops execution.

**When to enable:** per-module, on subscriber handlers that are not naturally idempotent. Not needed on internal handlers within the module (those are usually naturally idempotent or run inside a single transaction).

### 3. HTTP-level idempotency (client retry protection)

For write endpoints that clients might retry, use the `Idempotency-Key` header pattern.

**Middleware:**

```csharp
public sealed class IdempotencyKeyMiddleware(RequestDelegate next, HybridCache cache)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        if (!ctx.Request.Method.IsWriteMethod())
        {
            await next(ctx);
            return;
        }

        if (!ctx.Request.Headers.TryGetValue("Idempotency-Key", out var key))
        {
            await next(ctx);
            return;
        }

        var userId = ctx.User.GetUserId()?.ToString() ?? "anonymous";
        var cacheKey = $"idempotency:{userId}:{key}";

        var cached = await cache.GetOrCreateAsync<CachedResponse?>(
            cacheKey,
            factory: async _ =>
            {
                using var captured = new CapturingResponseBody(ctx);
                await next(ctx);
                return await captured.CaptureAsync();
            },
            options: new HybridCacheEntryOptions { Expiration = TimeSpan.FromHours(24) });

        if (cached is not null)
        {
            await cached.ReplayAsync(ctx);
        }
    }
}
```

(Production-quality version needs request fingerprinting: store the hash of the request body with the cached response, and return 422 if a retry uses the same key with a different body.)

**Registration:**

```csharp
app.UseMiddleware<IdempotencyKeyMiddleware>();
```

**Per-endpoint opt-in:** the middleware only activates when the header is present, which means the client decides. You can also require it for specific endpoints via a filter.

---

## Recommended strategy by use case

| Use case | Recommended |
|---|---|
| Internal CRUD API, trusted clients | Natural idempotency only |
| Public API with retrying clients | Natural + HTTP-level |
| Payments, provisioning | Natural + HTTP-level + handler-level |
| Event-driven backend with cross-module events | Natural + handler-level on subscribers |
| Webhooks | HTTP-level at the receiving endpoint |

---

## Testing idempotency

### Natural idempotency

```csharp
[Fact]
public async Task RecordingSameLoginTwice_DoesNotDuplicate()
{
    var loginId = Guid.NewGuid();
    await _handler.Handle(new RecordLogin(userId, loginId, DateTimeOffset.UtcNow), ct);
    await _handler.Handle(new RecordLogin(userId, loginId, DateTimeOffset.UtcNow), ct);

    var count = await _db.Logins.CountAsync(l => l.LoginId == loginId);
    count.ShouldBe(1);
}
```

### Handler-level

```csharp
[Fact]
public async Task DeliveringSameMessageTwice_InvokesHandlerOnce()
{
    var envelope = new Envelope(new OrderPlacedV1(...)) { Id = Guid.NewGuid() };

    await _host.InvokeMessageAndWaitAsync(envelope);
    await _host.InvokeMessageAndWaitAsync(envelope);

    (await _db.Orders.CountAsync(...)).ShouldBe(1);
}
```

### HTTP-level

```csharp
[Fact]
public async Task PostingWithSameIdempotencyKey_ReturnsCachedResponse()
{
    var key = Guid.NewGuid().ToString();
    var client = fixture.AuthenticatedClient().AsUser("alice").Build();
    client.DefaultRequestHeaders.Add("Idempotency-Key", key);

    var r1 = await client.PostAsJsonAsync("/v1/orders", validRequest);
    var r2 = await client.PostAsJsonAsync("/v1/orders", validRequest);

    r1.StatusCode.ShouldBe(HttpStatusCode.Created);
    r2.StatusCode.ShouldBe(HttpStatusCode.Created);
    (await r1.Content.ReadAsStringAsync()).ShouldBe(await r2.Content.ReadAsStringAsync());

    (await fixture.QueryDb<OrdersDbContext>(db => db.Orders.CountAsync())).ShouldBe(1);
}
```

---

## Common mistakes

- **Storing idempotency keys without TTL.** Unbounded growth. Always expire.
- **Using the request body hash without the key.** Two clients with identical requests get collisions.
- **Using the key without the user.** One client's key collides with another's.
- **Forgetting to rollback the processed-message row on failure.** The transaction scope handles this — if the handler fails, the processed-message insert is rolled back too. But only if you actually use the transaction Wolverine provides.
- **Thinking the outbox makes handlers idempotent.** The outbox guarantees at-least-once *delivery*. Idempotency is a separate concern.

---

## Related

- [`../adr/0003-wolverine-for-messaging.md`](../adr/0003-wolverine-for-messaging.md)
- [`../adr/0020-no-idempotency-infrastructure.md`](../adr/0020-no-idempotency-infrastructure.md)
- [`../adr/0017-hybrid-cache.md`](../adr/0017-hybrid-cache.md)
