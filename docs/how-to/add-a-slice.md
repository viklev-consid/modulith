# How-to: Add a Feature Slice

A slice is a single feature inside a module. Six files, co-located. This guide walks through adding one.

For the architectural reasoning, see [`adr/0002-vertical-slice-architecture.md`](../adr/0002-vertical-slice-architecture.md).

---

## Prerequisites

- The target module exists. (If not, see [`add-a-module.md`](add-a-module.md).)
- You know what the feature does: the HTTP verb, the route, the input, the output, and the state changes.
- You know which aggregate(s) are involved.

---

## The scaffold (preferred)

```bash
dotnet new slice --module Orders --name CancelOrder --verb Post
```

This produces six files under `src/Modules/Orders/Modulith.Modules.Orders/Features/CancelOrder/`:

- `CancelOrder.Request.cs`
- `CancelOrder.Response.cs`
- `CancelOrder.Command.cs`
- `CancelOrder.Handler.cs`
- `CancelOrder.Validator.cs`
- `CancelOrder.Endpoint.cs`

Plus an integration test stub:

- `tests/Modules/Orders/Modulith.Modules.Orders.IntegrationTests/Features/CancelOrderTests.cs`

All with correct namespaces and placeholder content.

---

## Doing it manually

Create the folder `Features/<FeatureName>/` in the module.

### 1. Request (HTTP input)

```csharp
// CancelOrder.Request.cs
namespace Modulith.Modules.Orders.Features.CancelOrder;

public sealed record CancelOrderRequest(string Reason);
```

- `sealed record` — required by architectural test.
- Plain primitives for wire types (`string`, not domain types).

### 2. Response (HTTP output)

```csharp
// CancelOrder.Response.cs
namespace Modulith.Modules.Orders.Features.CancelOrder;

public sealed record CancelOrderResponse(Guid OrderId, string Status, DateTimeOffset CancelledAt);
```

### 3. Command (internal message)

```csharp
// CancelOrder.Command.cs
using Modulith.Modules.Orders.Domain;

namespace Modulith.Modules.Orders.Features.CancelOrder;

internal sealed record CancelOrderCommand(OrderId OrderId, string Reason);
```

- `internal` — not exposed outside the module.
- Uses typed IDs (`OrderId`), not raw `Guid`. Mapping happens in the endpoint.

### 4. Handler

```csharp
// CancelOrder.Handler.cs
using ErrorOr;
using Modulith.Modules.Orders.Domain;
using Modulith.Modules.Orders.Persistence;

namespace Modulith.Modules.Orders.Features.CancelOrder;

internal sealed class CancelOrderHandler
{
    private readonly OrdersDbContext _db;

    public CancelOrderHandler(OrdersDbContext db) => _db = db;

    public async Task<ErrorOr<CancelOrderResponse>> Handle(CancelOrderCommand cmd, CancellationToken ct)
    {
        var order = await _db.Orders.FindAsync([cmd.OrderId], ct);
        if (order is null)
            return Errors.Orders.NotFound(cmd.OrderId);

        var result = order.Cancel(cmd.Reason);
        if (result.IsError)
            return result.Errors;

        return new CancelOrderResponse(
            cmd.OrderId.Value,
            order.Status.ToString(),
            order.CancelledAt!.Value);
    }
}
```

Key points:

- Return `ErrorOr<T>`, never throw for expected failures.
- Errors come from a module-local `Errors` static class (canonical set per module).
- Wolverine's `AutoApplyTransactions` policy wraps the handler in a DB transaction automatically.
- Integration events are raised by the aggregate; Wolverine's outbox publishes them post-commit.

### 5. Validator

```csharp
// CancelOrder.Validator.cs
using FluentValidation;

namespace Modulith.Modules.Orders.Features.CancelOrder;

internal sealed class CancelOrderValidator : AbstractValidator<CancelOrderRequest>
{
    public CancelOrderValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty()
            .MaximumLength(500);
    }
}
```

- Validates the `Request`, not the `Command`. The request is the shape of what arrives over the wire.
- Request-level concerns only — format, presence, length. Business invariants live in the aggregate.

### 6. Endpoint

```csharp
// CancelOrder.Endpoint.cs
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Modulith.Modules.Orders.Domain;
using Wolverine;

namespace Modulith.Modules.Orders.Features.CancelOrder;

internal static class CancelOrderEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/v{version:apiVersion}/orders/{id:guid}/cancel",
            async (Guid id, CancelOrderRequest req, IMessageBus bus, CancellationToken ct) =>
            {
                var command = new CancelOrderCommand(new OrderId(id), req.Reason);
                var result = await bus.InvokeAsync<ErrorOr<CancelOrderResponse>>(command, ct);
                return result.ToProblemDetailsOr(Results.Ok);
            })
        .WithName("CancelOrder")
        .WithSummary("Cancel an order that has not yet shipped.")
        .Produces<CancelOrderResponse>(200)
        .ProducesProblem(404)
        .ProducesProblem(409)
        .ProducesValidationProblem(422)
        .RequireAuthorization()
        .RequireRateLimiting("write")
        .MapToApiVersion(1);
}
```

- Endpoint depends only on `IMessageBus`. No direct handler dependency.
- `ToProblemDetailsOr(Results.Ok)` is the shared extension that maps `ErrorOr` to HTTP (200 on success, appropriate ProblemDetails on failure).
- OpenAPI metadata (`Produces`, `ProducesProblem`) is explicit — drives the Scalar docs.
- Rate limit policy applied by name.

### 7. Register the endpoint

In the module's `OrdersModule.cs`:

```csharp
public static IEndpointRouteBuilder MapOrdersEndpoints(this IEndpointRouteBuilder app)
{
    PlaceOrderEndpoint.Map(app);
    CancelOrderEndpoint.Map(app);   // ← add
    GetOrderByIdEndpoint.Map(app);
    return app;
}
```

### 8. Write the integration test

In `tests/Modules/Orders/Modulith.Modules.Orders.IntegrationTests/Features/CancelOrderTests.cs`:

```csharp
[Collection("OrdersModule")]
public sealed class CancelOrderTests(OrdersApiFixture fixture)
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task CancellingDraftOrder_ReturnsOkAndPublishesEvent()
    {
        // Arrange
        var order = await fixture.SeedAsync(OrderMother.Draft());
        var client = fixture.AuthenticatedClient().AsUser("alice").Build();

        // Act
        var response = await client.PostAsJsonAsync(
            $"/v1/orders/{order.Id.Value}/cancel",
            new CancelOrderRequest("Changed my mind"));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CancelOrderResponse>();
        body.ShouldNotBeNull();
        body.Status.ShouldBe("Cancelled");

        // Verify the event was published
        fixture.Tracker.Published<OrderCancelledV1>().ShouldNotBeEmpty();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CancellingShippedOrder_ReturnsConflict()
    {
        // Arrange
        var order = await fixture.SeedAsync(OrderMother.Shipped());
        var client = fixture.AuthenticatedClient().AsUser("alice").Build();

        // Act
        var response = await client.PostAsJsonAsync(
            $"/v1/orders/{order.Id.Value}/cancel",
            new CancelOrderRequest("Too late"));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }
}
```

### 9. Build, test, commit

```bash
dotnet build
dotnet test --filter "Category!=Smoke"
```

---

## Common mistakes

- **Forgetting to register the endpoint.** The file exists, the code compiles, but there's no route. Integration test fails with 404.
- **Injecting `IConfiguration` in the handler.** Use `IOptions<T>` with validation. Arch test will catch it.
- **Throwing instead of returning `Result`.** The endpoint won't map exceptions to ProblemDetails — they become 500s. Return `ErrorOr` failures for expected paths.
- **Validator on the Command instead of the Request.** Validator runs on HTTP input, before the Command is constructed. Put it on the Request.
- **Cross-module data in the handler.** If the handler needs data from another module, send a query via `IMessageBus`. Don't reach into another DbContext.
- **Skipping the integration test.** The fast tier doesn't exercise this slice. Integration tests are where contract regressions are caught.

---

## Related

- [`add-a-module.md`](add-a-module.md)
- [`cross-module-events.md`](cross-module-events.md)
- [`write-integration-test.md`](write-integration-test.md)
- [`handle-failures.md`](handle-failures.md)
