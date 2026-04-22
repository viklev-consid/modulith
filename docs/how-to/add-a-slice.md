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
dotnet new modulith-slice --module Orders --name CancelOrder
```

This produces six files under `src/Modules/Orders/Modulith.Modules.Orders/Features/CancelOrder/`:

- `CancelOrder.Request.cs`
- `CancelOrder.Response.cs`
- `CancelOrder.Command.cs`
- `CancelOrder.Handler.cs`
- `CancelOrder.Validator.cs`
- `CancelOrder.Endpoint.cs`

All with correct namespaces and stub content. The integration test file must be written manually — see Step 8 below.

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
            return OrdersErrors.NotFound(cmd.OrderId);

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
- Errors come from the module's `{Module}Errors` static class in `Errors/{Module}Errors.cs`. No inline strings.
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
        app.MapPost(OrdersRoutes.CancelOrder,
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
- Route strings come from `{Module}Routes` — a module-level constants file at `src/Modules/{Module}/Modulith.Modules.{Module}/{Module}Routes.cs`. Never inline route strings.
- `ToProblemDetailsOr(Results.Ok)` is the shared extension that maps `ErrorOr` to HTTP (200 on success, appropriate ProblemDetails on failure).
- OpenAPI metadata (`Produces`, `ProducesProblem`) is explicit — drives the Scalar docs.
- Rate limit policy applied by name.

### Add or update route constants

Routes live in a module-level `{Module}Routes.cs`, not inlined in endpoint files. If the file doesn't exist yet, create it:

```csharp
// src/Modules/Orders/Modulith.Modules.Orders/OrdersRoutes.cs
namespace Modulith.Modules.Orders;

internal static class OrdersRoutes
{
    public const string GroupTag = "Orders";
    public const string Prefix = "/v1/orders";
    public const string PlaceOrder = Prefix;
    public const string CancelOrder = $"{Prefix}/{{id:guid}}/cancel";
    public const string GetOrderById = $"{Prefix}/{{id:guid}}";
}
```

If the file already exists, add the new route constant to it.

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

## Resource-level authorization (ownership checks)

Some slices need to allow different callers different levels of access to the same resource — for example, an admin can read any user's audit trail while a regular user can read only their own. This is **resource-based authorization** and it lives in the **endpoint**, not the handler.

Keeping the check in the endpoint preserves handler purity: if the query is in a `.Contracts` project and can be invoked by other modules or background jobs, placing `ICurrentUser` in the handler would silently break those non-HTTP callers. The endpoint is the HTTP boundary; it is the right place to enforce HTTP-caller-specific authorization.

### The pattern

Two types in `Shared.Infrastructure.Authorization`:

- **`IResourcePolicy<TResource>`** — determines whether the current caller may access a specific resource instance.
- **`PermissionOrOwnerPolicy<TResource>`** — covers the common case: elevated permission → full access; no permission → ownership check.

### Adding a policy for a new resource

**1. Define a resource type** (or reuse an existing entity). If protecting a list query, a lightweight scope record is enough:

```csharp
// Orders module — internal record representing the scope being protected
internal sealed record OrderResource(Guid OwnerId);
```

**2. Implement the policy** in the module's `Authorization/` folder:

```csharp
internal sealed class OrderPolicy : PermissionOrOwnerPolicy<OrderResource>
{
    protected override string ElevatedPermission => OrdersPermissions.OrdersRead;
    protected override string? GetOwnerId(OrderResource r) => r.OwnerId.ToString();
}
```

For rules that can't be expressed as a single elevated permission + owner ID (multi-tenant membership, delegated access, etc.) implement `IResourcePolicy<TResource>` directly.

**3. Register in the module's DI setup:**

```csharp
services.AddSingleton<IResourcePolicy<OrderResource>, OrderPolicy>();
```

**4. Apply in the endpoint:**

```csharp
app.MapGet(OrdersRoutes.GetOrder,
    async (
        ICurrentUser currentUser,
        IResourcePolicy<OrderResource> policy,
        IMessageBus bus,
        CancellationToken ct,
        Guid orderId) =>
    {
        // Ownership check at the HTTP boundary — the handler stays pure and
        // callable by internal/background callers without an HTTP user context.
        var resource = new OrderResource(orderId);
        if (!policy.IsAuthorized(currentUser, resource))
            return Results.Forbid();

        var query = new GetOrderQuery(orderId);
        var result = await bus.InvokeAsync<ErrorOr<GetOrderResponse>>(query, ct);
        return result.ToProblemDetailsOr(Results.Ok);
    })
   .RequireAuthorization()
   .RequireRateLimiting("read");
```

**5. Keep the handler pure.** The handler receives only what it needs to execute the query — no `ICurrentUser`, no policy:

```csharp
public sealed class GetOrderHandler(OrdersDbContext db)
{
    private async Task<ErrorOr<GetOrderResponse>> HandleCoreAsync(GetOrderQuery query, CancellationToken ct)
    {
        var order = await db.Orders.FindAsync([query.OrderId], ct);
        if (order is null)
            return OrdersErrors.NotFound;

        // ...
    }
}
```

### When to use `PermissionOrOwnerPolicy` vs. a direct implementation

| Scenario | Use |
|---|---|
| Elevated permission = full access; else = owner only | `PermissionOrOwnerPolicy<T>` |
| Multi-field ownership (e.g. org membership) | Implement `IResourcePolicy<T>` directly |
| No ownership concept — purely permission-gated | `RequireAuthorization(SomePermissions.Const)` on the endpoint, no policy needed |

---

## Related

- [`add-a-module.md`](add-a-module.md)
- [`cross-module-events.md`](cross-module-events.md)
- [`write-integration-test.md`](write-integration-test.md)
- [`handle-failures.md`](handle-failures.md)
