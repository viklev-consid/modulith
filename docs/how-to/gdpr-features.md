# How-to: GDPR Features

Modulith ships with primitives for GDPR compliance — classification, export, erasure, consent, retention. This guide covers how to use them when adding features.

For the decisions, see [`../adr/0012-gdpr-primitives.md`](../adr/0012-gdpr-primitives.md). For high-level compliance posture, see [`../../COMPLIANCE.md`](../../COMPLIANCE.md).

---

## Classifying personal data

Mark any property that holds personal data:

```csharp
public sealed class User : AggregateRoot<UserId>
{
    [PersonalData] public Email Email { get; private set; }
    [PersonalData] public string DisplayName { get; private set; }
    [SensitivePersonalData] public string? PhoneNumber { get; private set; }
    // Non-personal fields: no attribute
    public DateTimeOffset CreatedAt { get; private set; }
}
```

`[PersonalData]` drives:
- **Log masking** — Serilog replaces the value with `***` in log output.
- **Export inclusion** — the personal data exporter includes classified fields.
- **Documentation** — a data map can be generated from attributes.

`[SensitivePersonalData]` is stricter:
- Same behavior, but flagged separately for tighter handling (e.g., encryption at rest as an extension point).

**Classify at the boundary that matters.** A `User` aggregate holds personal data; its DTO representation does too; an audit entry that carries a user's email does too. Classify consistently.

---

## Implementing `IPersonalDataExporter`

Each module that holds personal data implements the exporter:

```csharp
// src/Modules/Orders/Modulith.Modules.Orders/Gdpr/OrdersPersonalDataExporter.cs
internal sealed class OrdersPersonalDataExporter : IPersonalDataExporter
{
    private readonly OrdersDbContext _db;

    public OrdersPersonalDataExporter(OrdersDbContext db) => _db = db;

    public async Task<PersonalDataExport> ExportAsync(UserRef user, CancellationToken ct)
    {
        var orders = await _db.Orders
            .Where(o => o.CustomerId == user.UserId)
            .Select(o => new
            {
                o.Id,
                o.PlacedAt,
                o.Total,
                Items = o.Lines.Select(l => new { l.Sku, l.Quantity, l.UnitPrice })
            })
            .ToListAsync(ct);

        return new PersonalDataExport(
            Module: "Orders",
            Categories: ["order-history"],
            Data: orders);
    }
}
```

Register:

```csharp
services.AddScoped<IPersonalDataExporter, OrdersPersonalDataExporter>();
```

The Users module exposes an aggregator endpoint:

```
GET /v1/users/me/personal-data
```

The aggregator invokes all registered exporters and returns a combined JSON package.

---

## Implementing `IPersonalDataEraser`

Each module that holds personal data implements the eraser. Strategy is module-specific:

```csharp
// src/Modules/Orders/Modulith.Modules.Orders/Gdpr/OrdersPersonalDataEraser.cs
internal sealed class OrdersPersonalDataEraser : IPersonalDataEraser
{
    private readonly OrdersDbContext _db;
    private readonly HybridCache _cache;

    public OrdersPersonalDataEraser(OrdersDbContext db, HybridCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<ErasureResult> EraseAsync(
        UserRef user, ErasureStrategy strategy, CancellationToken ct)
    {
        // Orders module chooses: anonymize rather than hard-delete (retention for accounting)
        var affected = await _db.Orders
            .Where(o => o.CustomerId == user.UserId)
            .ExecuteUpdateAsync(s =>
                s.SetProperty(o => o.CustomerId, CustomerId.Anonymized)
                 .SetProperty(o => o.CustomerDisplayName, "[erased]"), ct);

        // Flush related cache entries
        await _cache.RemoveByTagAsync($"orders:customer:{user.UserId}", ct);

        return new ErasureResult(
            Module: "Orders",
            Strategy: "anonymize",
            RecordsAffected: affected);
    }
}
```

Different modules choose different strategies:

- **Users**: hard-delete the aggregate, but keep a tombstone (`DeletedAt`, `DeletionReason`).
- **Orders**: anonymize (retain the record, scrub personal fields) — regulatory retention requires order records.
- **Audit**: anonymize the actor — audit trail must persist but shouldn't identify individuals after erasure.
- **Notifications**: hard-delete logs, hard-delete preferences.

Register:

```csharp
services.AddScoped<IPersonalDataEraser, OrdersPersonalDataEraser>();
```

**Arch test:** modules with entities referencing a `UserId` must have either an `IPersonalDataEraser` or a `[NoPersonalData]` assembly attribute.

---

## The erasure flow

1. User invokes `DELETE /v1/users/me`.
2. Users module validates the request (consent, identity, any pending obligations).
3. Users module publishes `UserErasureRequestedV1`.
4. Each module subscribes via its `IPersonalDataEraser`.
5. Erasers run in parallel; each records the result.
6. Users module aggregates results into an `ErasureCompleted` record.
7. User receives a confirmation with an erasure reference ID.

Eventually consistent — erasure is not instantaneous. The user sees a "request accepted" response; an email confirms completion typically within minutes.

---

## Consent

The Users module owns a `Consents` table:

```csharp
public sealed class Consent : Entity<ConsentId>
{
    public UserId UserId { get; private set; }
    public ConsentPurpose Purpose { get; private set; }       // e.g., "marketing-emails"
    public DateTimeOffset? GrantedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public string PolicyVersion { get; private set; }          // which version of the policy
}
```

Other modules check consent via a service:

```csharp
public interface IConsentRegistry
{
    Task<bool> IsGrantedAsync(UserId userId, ConsentPurpose purpose, CancellationToken ct);
}
```

Used by the Notifications module when deciding whether to send marketing emails.

**Consent != notification preference.** Consent is legal intent (did the user agree?). Notification preferences are UX (do they want it right now?). Consent is in Users; preferences are in Notifications. Tied together: revoking consent flips the corresponding preference off automatically (via an integration event from Users to Notifications).

---

## Retention

Entities that should expire implement `IRetainable`:

```csharp
public sealed class NotificationLogEntry : Entity<Guid>, IRetainable
{
    public DateTimeOffset SentAt { get; private set; }

    public TimeSpan RetentionPeriod => TimeSpan.FromDays(365);
    public DateTimeOffset RetentionStartsAt => SentAt;
}
```

A scheduled Wolverine job sweeps `IRetainable` entities past their period. Each module implements its sweep logic (anonymize / delete / archive).

Don't set retention policies without legal input. The defaults ship for *illustration*, not compliance advice.

---

## Cache flushing on erasure

`IPersonalDataEraser` implementations **must** flush any cache keys related to the user. Otherwise erased data resurfaces from cache for the key's TTL.

Pattern:

```csharp
await _cache.RemoveByTagAsync($"user:{userId}", ct);
```

Use tagged cache entries for user-scoped data so tag-based invalidation works without enumerating keys.

---

## Common mistakes

- **Unmarked personal data.** Leaks through logs, not included in exports.
- **Hard-delete where retention is required.** Regulatory retention beats GDPR in some cases — talk to legal.
- **Erasure that leaves dangling foreign keys.** No cross-schema FKs (per ADR-0023), but within a module, an erasure that leaves `UserId` pointing at a deleted user is a bug.
- **Forgetting to flush caches.** The user's data reappears for the cache TTL.
- **Treating consent and preferences as the same.** Distinct concepts; distinct storage; related but not equal.
- **Implementing erasure but not export.** Both are GDPR rights. Ship together.
- **Shipping without testing the erasure flow end-to-end.** Integration test: register user → place data → erase → confirm nothing remains (except retention-preserved anonymized records).

---

## Compliance boundaries

This template reduces technical risk for GDPR compliance. It does **not** constitute legal advice. Your team is responsible for:

- Determining retention periods with legal.
- Writing the privacy policy.
- Responding to regulatory inquiries.
- Reviewing cross-border transfer (deployment-specific).
- Ensuring backups are covered by retention policies.

See [`../../COMPLIANCE.md`](../../COMPLIANCE.md) for the full posture.

---

## Related

- [`../adr/0012-gdpr-primitives.md`](../adr/0012-gdpr-primitives.md)
- [`../adr/0010-serilog-and-otel.md`](../adr/0010-serilog-and-otel.md)
- [`../adr/0011-auditing-strategy.md`](../adr/0011-auditing-strategy.md)
- [`../../COMPLIANCE.md`](../../COMPLIANCE.md)
