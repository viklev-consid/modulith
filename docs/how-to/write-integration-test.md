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
- Exposing `AuthenticatedClient()` for JWT-backed HTTP calls.
- Exposing `QueryDb<TContext>(...)` for verification queries.
- Exposing `Tracker` / `TrackActivity(...)` for Wolverine message assertions.
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
        var customer = await fixture.SeedAsync(UserMother.Active());
        var product = await fixture.SeedAsync<CatalogDbContext>(ProductMother.InStock(sku: "SKU-1"));
        var client = fixture.AuthenticatedClient().AsUser(customer).Build();

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
        var order = await fixture.QueryDb<OrdersDbContext>(db =>
            db.Orders.SingleOrDefaultAsync(o => o.Id == new OrderId(body.OrderId)));
        order.ShouldNotBeNull();
        order.Status.ShouldBe(OrderStatus.Placed);
        order.Lines.Count.ShouldBe(1);

        // Assert — events (if relevant)
        fixture.Tracker.Published<OrderPlacedV1>().ShouldContain(e => e.OrderId == body.OrderId);
    }
}
```

---

## Phase breakdown

### Arrange

Use object mothers from `TestSupport.TestDataBuilders`. They produce valid aggregates and persist them via `fixture.SeedAsync(...)`. This exercises your real factory methods — stale mothers fail fast.

Build the authenticated client:

```csharp
var client = fixture.AuthenticatedClient()
    .AsUser("alice")            // or AsUser(userAggregate)
    .WithRoles("Admin")          // optional
    .Build();
```

Unauthenticated requests:

```csharp
var client = fixture.AnonymousClient();
```

### Act

Call the endpoint via the client. Prefer strongly-typed bodies (`PostAsJsonAsync`, `PutAsJsonAsync`) for write endpoints; response comes back as `HttpResponseMessage`.

For flows that publish cross-module events, use `TrackActivity`:

```csharp
var session = await fixture.Host.TrackActivity()
    .Timeout(TimeSpan.FromSeconds(10))
    .ExecuteAndWaitAsync(async () =>
    {
        var response = await client.PostAsJsonAsync("/v1/orders", request);
        response.EnsureSuccessStatusCode();
    });
```

`TrackActivity` blocks until all cascading messages finish — no sleeps, no polling.

### Assert

Three categories of assertions:

1. **HTTP response.** Status code, body shape, headers.
2. **Persistence state.** Read back from the DbContext via `QueryDb<TContext>(...)`.
3. **Side-effect messages.** `fixture.Tracker.Published<T>()` or `session.Executed.SingleMessage<T>()`.

Keep assertions tight to the scenario. A happy-path test asserting five different persistence details is a smell.

---

## Snapshot testing contracts

For responses and events that are part of a public contract, snapshot with Verify:

```csharp
[Fact]
[Trait("Category", "Integration")]
public async Task PlaceOrderResponse_MatchesSnapshot()
{
    var body = await PlaceValidOrderAndReturnBody();
    await Verify(body);
}
```

The first run creates `PlaceOrderResponse_MatchesSnapshot.verified.json`. Subsequent runs fail if the shape changes, prompting a review-and-accept. Volatile fields (timestamps, IDs) are scrubbed via shared Verify settings in `TestSupport`.

---

## Assertions on validation failures

```csharp
[Fact]
[Trait("Category", "Integration")]
public async Task PlacingOrderWithNoItems_ReturnsValidationProblem()
{
    var client = fixture.AuthenticatedClient().AsUser("alice").Build();
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
    var order = await fixture.SeedAsync(OrderMother.Shipped());
    var client = fixture.AuthenticatedClient().AsUser("alice").Build();

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

WireMock.Net, configured via `TestSupport.WireMockFixture`:

```csharp
[Fact]
[Trait("Category", "Integration")]
public async Task PlacingOrder_CallsPaymentProvider()
{
    fixture.WireMock
        .Given(Request.Create().WithPath("/payments/charge").UsingPost())
        .RespondWith(Response.Create().WithStatusCode(200).WithBodyAsJson(new { id = "ch_123" }));

    // ... act and assert
}
```

The fixture configures the application to point at the WireMock URL via `IOptions` override.

---

## Controlling time

Use `TestClock` from `TestSupport`, which implements `IClock` from `Shared.Kernel`:

```csharp
fixture.Clock.Set(new DateTimeOffset(2025, 1, 15, 12, 0, 0, TimeSpan.Zero));
// ... perform operation ...
fixture.Clock.Advance(TimeSpan.FromHours(1));
```

Never use `DateTime.UtcNow` in handlers — always inject `IClock`. Makes time-sensitive tests deterministic.

---

## Common mistakes

- **Using a raw `HttpClient` instead of the authenticated builder.** Tests look simpler but break when auth requirements change.
- **Asserting on `title` or `detail` of ProblemDetails.** Human-readable text; use `errorCode`.
- **Sleeping instead of `TrackActivity`.** Flaky tests. `TrackActivity` is always the right answer for Wolverine flows.
- **Seeding by writing raw entities.** Use object mothers — they go through factory methods and catch domain invariant drift.
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
