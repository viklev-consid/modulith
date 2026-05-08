# How-to: Write an Integration Test

Integration tests exercise one module's slice end-to-end: HTTP in, database and events out. This guide walks through the standard pattern.

For the full testing strategy, see [`../testing-strategy.md`](../testing-strategy.md). For the underlying decisions, see [`../adr/0022-testing-strategy.md`](../adr/0022-testing-strategy.md).

---

## When to write an integration test

Every slice should have at least one integration test for the happy path. Additional tests for:

- Validation rejection (422)
- Not-found responses (404)
- Authorization failures (401, 403)
- Business rule violations (409 Conflict)
- Cross-module event flows (that the event is actually published and handled)
- Persistence side effects (state after command matches expectation)

Do NOT write integration tests for:

- Pure domain logic (that's unit tests).
- Structural rules (that's architectural tests).
- Framework behavior (that's framework tautology).

---

## The fixture

Each module has its own fixture (inheriting `ApiTestFixture` from `TestSupport`):

```csharp
// tests/Modules/Orders/Modulith.Modules.Orders.IntegrationTests/OrdersApiFixture.cs
public sealed class OrdersApiFixture : ApiTestFixture
{
    // Module-specific overrides if needed.
    // Most modules need nothing beyond the base.
}

[CollectionDefinition("OrdersModule")]
public sealed class OrdersModuleCollection : ICollectionFixture<OrdersApiFixture> { }
```

The base `ApiTestFixture` handles:

- Starting a Postgres Testcontainer.
- Running migrations once.
- Building a `WebApplicationFactory<Program>`.
- Exposing `CreateAnonymousClient()` and `CreateAuthenticatedClient(...)` for HTTP calls.
- Exposing `CreateAuthenticatedClientBuilder()` when custom claims make a scenario clearer.
- Exposing `CreateAuthenticatedClientWithToken(...)` for tokens issued by the real auth flow.
- Exposing `QueryDbAsync<TDbContext, TResult>()`, `ExecuteDbAsync<TDbContext>()`, and `SeedDbAsync<TDbContext>()` for scoped DbContext setup and assertions.
- Exposing `TrackWolverineActivityAsync(...)` as the default wrapper around Wolverine's `TrackActivity()` for message assertions.
- Respawn-based cleanup between tests.

---

## The canonical test shape

```csharp
[Collection("OrdersModule")]
public sealed class PlaceOrderTests(OrdersApiFixture fixture)
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task PlacingValidOrder_ReturnsCreatedAndPersistsOrder()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var client = fixture.CreateAuthenticatedClient(userId, "alice@example.com", "Alice");

        var request = new PlaceOrderRequest(
            Items: [new PlaceOrderRequest.Item("SKU-1", Quantity: 2)]);

        // Act
        var response = await client.PostAsJsonAsync("/v1/orders", request);

        // Assert — HTTP
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<PlaceOrderResponse>();
        body.ShouldNotBeNull();
        body.OrderId.ShouldNotBe(Guid.Empty);

        // Assert — persistence
        var order = await fixture.QueryDbAsync<OrdersDbContext, Order?>(db =>
            db.Orders.SingleOrDefaultAsync(o => o.Id == new OrderId(body.OrderId)));

        order.ShouldNotBeNull();
        order.Status.ShouldBe(OrderStatus.Placed);
        order.Lines.Count.ShouldBe(1);
    }
}
```

---

## Phase breakdown

### Arrange

Prefer arranging data through the module's public API. When a scenario really needs direct setup, use `SeedDbAsync<TDbContext>` and aggregate factory methods so invariants are still exercised.

Build the authenticated client:

```csharp
var client = fixture.CreateAuthenticatedClient(
    Guid.NewGuid(),
    "alice@example.com",
    "Alice",
    role: "admin");
```

For custom claims:

```csharp
var client = fixture.CreateAuthenticatedClientBuilder()
    .WithUser(Guid.NewGuid(), "alice@example.com", "Alice")
    .WithRole("admin")
    .WithClaim("permission", "orders.manage")
    .Build();
```

Unauthenticated requests:

```csharp
var client = fixture.CreateAnonymousClient();
```

### Act

Call the endpoint via the client. Prefer strongly-typed bodies (`PostAsJsonAsync`, `PutAsJsonAsync`) for write endpoints; response comes back as `HttpResponseMessage`.

For flows that publish cross-module events, use `TrackWolverineActivityAsync`:

```csharp
var session = await fixture.TrackWolverineActivityAsync(async () =>
{
    var response = await client.PostAsJsonAsync("/v1/orders", request);
    response.EnsureSuccessStatusCode();
});
```

`TrackWolverineActivityAsync` blocks until all cascading messages finish — no sleeps, no polling. Use `assertOnExceptions: false` only for retry and dead-letter policy tests where exceptions are expected:

```csharp
var session = await fixture.TrackWolverineActivityAsync(
    async () =>
    {
        var response = await client.PostAsJsonAsync("/v1/orders", request);
        response.EnsureSuccessStatusCode();
    },
    assertOnExceptions: false);
```

### Assert

Three categories of assertions:

1. **HTTP response.** Status code, body shape, headers.
2. **Persistence state.** Read back from the DbContext through `QueryDbAsync`.
3. **Side-effect messages.** Use the session returned from `TrackWolverineActivityAsync`.

Keep assertions tight to the scenario. A happy-path test asserting five different persistence details is a smell.

---

## Snapshot testing contracts

If a response or event is important enough to review as a contract artifact, use Verify with the shared TestSupport settings:

```csharp
[Fact]
[Trait("Category", "Integration")]
public async Task PlaceOrderResponse_MatchesSnapshot()
{
    var body = await PlaceValidOrderAndReturnBody();
    await Verifier.Verify(body, VerifyTestSettings.Create());
}
```

For Verify, the first run creates `PlaceOrderResponse_MatchesSnapshot.verified.json`. Subsequent runs fail if the shape changes, prompting a review-and-accept. Add scenario-specific scrubbers to the returned settings when a contract includes volatile IDs.

---

## Assertions on validation failures

```csharp
[Fact]
[Trait("Category", "Integration")]
public async Task PlacingOrderWithNoItems_ReturnsValidationProblem()
{
    var client = fixture.CreateAuthenticatedClient(Guid.NewGuid(), "alice@example.com", "Alice");
    var request = new PlaceOrderRequest(Items: []);

    var response = await client.PostAsJsonAsync("/v1/orders", request);

    response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
    problem.ShouldNotBeNull();
    problem.Errors.ShouldContainKey("Items");
}
```

The `ValidationProblemDetails` shape is standard (`errors: { field: [messages] }`).

---

## Assertions on business-rule failures

```csharp
[Fact]
[Trait("Category", "Integration")]
public async Task CancellingShippedOrder_ReturnsConflictWithErrorCode()
{
    var client = fixture.CreateAuthenticatedClient(Guid.NewGuid(), "alice@example.com", "Alice");
    var order = await CreateShippedOrderAsync(fixture);

    var response = await client.PostAsJsonAsync(
        $"/v1/orders/{order.Id.Value}/cancel",
        new CancelOrderRequest("Too late"));

    response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
    problem.ShouldNotBeNull();
    problem.Extensions["errorCode"]?.ToString().ShouldBe("orders.cannot_cancel_shipped");
}
```

Clients pattern-match on `errorCode`, not on `title` or `detail`.

---

## Mocking external HTTP

WireMock.Net is the preferred tool for external HTTP. Use `WireMockFixture` inside a module fixture and configure the application to point at `wireMock.Url` via settings or options overrides.

```csharp
wireMock.Server
    .Given(Request.Create().WithPath("/payments/charge").UsingPost())
    .RespondWith(Response.Create().WithStatusCode(200).WithBodyAsJson(new { id = "ch_123" }));

// ... act and assert
```

---

## Controlling time

Use `TestClock` from `TestSupport`, which implements `IClock` from `Shared.Kernel`. Register and expose it from the module fixture that needs deterministic time:

```csharp
fixture.Clock.Set(new DateTimeOffset(2025, 1, 15, 12, 0, 0, TimeSpan.Zero));
// ... perform operation ...
fixture.Clock.Advance(TimeSpan.FromHours(1));
```

Never use `DateTime.UtcNow` in handlers — always inject `IClock`. Makes time-sensitive tests deterministic.

---

## Common mistakes

- **Crafting JWTs inline instead of using fixture helpers.** Tests look simpler but break when auth requirements change.
- **Asserting on `title` or `detail` of ProblemDetails.** Human-readable text; use `errorCode`.
- **Sleeping instead of `TrackActivity`.** Flaky tests. `TrackActivity` is always the right answer for Wolverine flows.
- **Seeding by bypassing aggregate factories.** Use public APIs or factory methods so tests catch domain invariant drift.
- **Not marking `[Trait("Category", "Integration")]`.** Test runs in the fast tier and makes it slow.
- **Sharing state between tests.** Respawn wipes between tests; shared state manifests as order-dependent failures.
- **Testing ASP.NET's routing.** `"When I POST to /v1/orders, I hit the PlaceOrder endpoint"` is not a test. Test what happens next.

---

## Running integration tests

```bash
# All integration tests
dotnet test --filter "Category=Integration"

# One module's
dotnet test tests/Modules/Orders/Modulith.Modules.Orders.IntegrationTests

# One class
dotnet test --filter "FullyQualifiedName~PlaceOrderTests"

# One test
dotnet test --filter "FullyQualifiedName~PlaceOrderTests.PlacingValidOrder_ReturnsCreatedAndPersistsOrder"
```

Docker must be running — Testcontainers requires it.

---

## Related

- [`add-a-slice.md`](add-a-slice.md)
- [`cross-module-events.md`](cross-module-events.md)
- [`../testing-strategy.md`](../testing-strategy.md)
- [`../adr/0022-testing-strategy.md`](../adr/0022-testing-strategy.md)
