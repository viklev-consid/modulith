---
name: vertical-slice
description: Canonical workflow for adding or modifying a vertical slice in Modulith. Covers scaffold, file layout, naming, Request -> Command or Query -> Validator -> Handler -> Endpoint flow, Wolverine registration, integration events, and required tests.
---

# Vertical Slice

Use this skill when you are adding or changing a feature under `src/Modules/<Module>/Modulith.Modules.<Module>/Features/<Feature>/`.

This skill is for the normal case: an existing module, a well-scoped feature, and no architecture change.

Do not use this skill when:

- the feature might belong to a new module
- the change is primarily about cross-module contract design
- the task is only a domain-model refactor under `Domain/`
- the task is only an EF Core migration

For those cases, stop and use the more specific workflow or ask first.

## Read first

Before writing code, read the repo guidance in this order:

1. `/CLAUDE.md`
2. `/src/Modules/CLAUDE.md`
3. `/src/Modules/<Module>/CLAUDE.md` if it exists
4. `docs/how-to/add-a-slice.md`
5. One nearby slice in the target module

If the slice will publish or consume cross-module events, also read:

- `docs/how-to/cross-module-events.md`
- `docs/adr/0003-wolverine-for-messaging.md`
- `docs/adr/0006-internal-vs-public-events.md`

If the slice changes persisted data shape, also read:

- `docs/how-to/work-with-migrations.md`

## Decide whether this is a command or query slice

Use a command slice when the feature changes state.

- POST, PUT, PATCH, DELETE usually map to commands
- the handler loads aggregates, invokes methods, saves changes, and may publish events

Use a query slice when the feature is read-only.

- GET usually maps to queries
- the handler should use `AsNoTracking()` and project directly in SQL
- a validator may be unnecessary if the endpoint only binds trivial primitives

If the feature both reads and writes, it is still a command slice. Return the response DTO from the handler after the write.

## Prefer scaffolding

Start with:

```bash
dotnet new modulith-slice --module <Module> --name <FeatureName>
```

This is preferred over manual creation. The scaffold gives you the expected file set and naming.

The canonical folder is:

```text
src/Modules/<Module>/Modulith.Modules.<Module>/Features/<FeatureName>/
```

## Canonical file set

For the normal command slice, create or update these files in the feature folder:

- `<FeatureName>.Request.cs`
- `<FeatureName>.Response.cs`
- `<FeatureName>.Command.cs`
- `<FeatureName>.Validator.cs`
- `<FeatureName>.Handler.cs`
- `<FeatureName>.Endpoint.cs`

Also expect to touch these non-slice files:

- `src/Modules/<Module>/Modulith.Modules.<Module>/<Module>Routes.cs`
- `src/Modules/<Module>/Modulith.Modules.<Module>/<Module>Module.cs`
- `tests/Modules/<Module>/Modulith.Modules.<Module>.IntegrationTests/Features/<FeatureName>Tests.cs`

For a simple query slice:

- use `<FeatureName>.Query.cs` instead of `.Command.cs`
- keep `.Response.cs`, `.Handler.cs`, and `.Endpoint.cs`
- omit `.Validator.cs` only when there is nothing meaningful to validate
- omit `.Request.cs` only when the endpoint binds a few trivial query-string primitives directly and the nearby module examples already do that

When in doubt, keep the full six-file shape.

## Type and naming rules

Apply these consistently:

- one public type per file
- file-scoped namespaces
- `public sealed record` for requests, responses, commands, and queries
- `public sealed class` for Wolverine handlers
- `internal static class` for endpoints
- `internal sealed class : AbstractValidator<TRequest>` for validators
- async methods end with `Async` when they are ordinary methods; Wolverine `Handle` methods stay `Handle`

Important: Wolverine handler types must be `public`. Do not make handlers `internal`.

## HTTP boundary rules

Requests and responses are wire types.

- use primitives and DTOs at the HTTP boundary
- do not expose domain value objects in requests, responses, or public integration events
- map route `Guid` values to typed IDs in the endpoint or command construction step

Keep route strings in `<Module>Routes.cs`. Do not inline them in endpoints.

## Canonical flow

The default flow is:

1. Request carries HTTP input as primitives.
2. Endpoint validates the request with FluentValidation.
3. Endpoint maps primitives to a command or query.
4. Endpoint dispatches through `IMessageBus` only.
5. Handler loads aggregates or queries the DbContext.
6. Handler calls domain methods or factories for business rules.
7. Handler saves changes when mutating state.
8. Handler publishes integration events if other modules care.
9. Handler returns `ErrorOr<TResponse>`.
10. Endpoint maps the result to HTTP via the shared ProblemDetails helper.

Endpoints must not depend directly on handlers, repositories, or domain services.

## File-by-file guidance

### Request

Use the request DTO for input coming from HTTP.

- keep it small and flat
- use primitives, not domain types
- reflect the wire contract, not the domain model

Example:

```csharp
namespace Modulith.Modules.Orders.Features.CancelOrder;

public sealed record CancelOrderRequest(string Reason);
```

### Response

Use the response DTO for the HTTP result.

- keep it stable and explicit
- return primitives and DTOs only
- do not leak EF entities or domain types

### Command or Query

Commands and queries are the internal message shape for Wolverine.

- use `Command` for state changes
- use `Query` for read-only flows
- use typed IDs here when the operation targets an existing aggregate
- keep the record focused on what the handler needs

Example command:

```csharp
namespace Modulith.Modules.Orders.Features.CancelOrder;

public sealed record CancelOrderCommand(OrderId OrderId, string Reason);
```

### Validator

Validate the request, not the command.

Keep validator rules at the request boundary:

- required fields
- lengths
- formats
- simple cross-field checks

Do not put business invariants here. Those belong in the domain model.

### Handler

The handler orchestrates; the aggregate enforces invariants.

Handler rules:

- return `ErrorOr<TResponse>`
- load aggregates from the module DbContext
- call value object factories before persisting when format validation belongs to the domain
- call aggregate methods or factory methods for business rules
- return module errors, not inline strings
- throw for unexpected bugs or infrastructure faults
- catch and convert only specific expected exceptions such as concurrency conflicts

For write handlers:

- add entities to the DbContext
- call `SaveChangesAsync(ct)`
- publish public integration events only after the save call

For read handlers:

- use `AsNoTracking()`
- project in SQL with `Select(...)`
- avoid materializing full entities if a projection will do

Example shape:

```csharp
public sealed class CancelOrderHandler(OrdersDbContext db)
{
    public async Task<ErrorOr<CancelOrderResponse>> Handle(CancelOrderCommand cmd, CancellationToken ct)
    {
        var order = await db.Orders.FindAsync([cmd.OrderId], ct);
        if (order is null)
        {
            return OrdersErrors.NotFound(cmd.OrderId);
        }

        var result = order.Cancel(cmd.Reason);
        if (result.IsError)
        {
            return result.Errors;
        }

        await db.SaveChangesAsync(ct);

        return new CancelOrderResponse(order.Id.Value, order.Status.ToString(), order.CancelledAt!.Value);
    }
}
```

### Endpoint

The endpoint is an adapter from HTTP to `IMessageBus`.

Endpoint rules:

- depend only on `IMessageBus` plus request-time services like validators
- validate the request before creating the command or query
- call `bus.InvokeAsync<ErrorOr<TResponse>>(...)`
- map the result with `ToProblemDetailsOr(...)`
- declare OpenAPI metadata explicitly
- apply auth and rate limiting as required by the module

Example shape:

```csharp
internal static class CancelOrderEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost(OrdersRoutes.CancelOrder,
            async (Guid id, CancelOrderRequest request, IValidator<CancelOrderRequest> validator, IMessageBus bus, CancellationToken ct) =>
            {
                var validation = await validator.ValidateAsync(request, ct);
                if (!validation.IsValid)
                {
                    return Results.ValidationProblem(validation.ToDictionary(), statusCode: StatusCodes.Status422UnprocessableEntity);
                }

                var command = new CancelOrderCommand(new OrderId(id), request.Reason);
                var result = await bus.InvokeAsync<ErrorOr<CancelOrderResponse>>(command, ct);
                return result.ToProblemDetailsOr(Results.Ok);
            })
        .WithName("CancelOrder")
        .WithSummary("Cancel an order that has not yet shipped.")
        .Produces<CancelOrderResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);
}
```

## Errors and failures

Expected failures return `ErrorOr` errors.

- not found -> module `NotFound` error
- validation -> validator failure or module validation error
- business rule violation -> conflict or validation error
- forbidden or unauthorized -> explicit error type

Do not inline error strings in handlers or aggregates. Use the module error catalog under `Errors/<Module>Errors.cs`.

Do not throw for expected failures.

## Wolverine and domain events

There are two different event types in this codebase:

1. internal domain events raised inside aggregates
2. public integration events defined in `<Module>.Contracts/Events`

Do not reuse the same type for both.

If the change matters only inside the module:

- raise an internal domain event from the aggregate
- handle it inside the module

If other modules care:

- define a versioned public event such as `OrderCancelledV1` in the `.Contracts` project
- publish it via `IMessageBus`
- rely on Wolverine's outbox for post-commit delivery

Publishing rule:

- save state first with `SaveChangesAsync`
- then call `bus.PublishAsync(...)`
- Wolverine persists the outgoing message transactionally and publishes it after commit

Subscriber rule:

- subscriber handlers live in `Integration/`
- subscriber handlers must be `public`
- subscribers must be idempotent because delivery is at-least-once

If you are unsure whether the boundary should be an event, a query, or a command, stop and use the module-boundary workflow before coding.

## Required registration steps

A new slice is not finished until it is wired into the module.

### Routes

Add or update the route constant in `<Module>Routes.cs`.

### Wolverine handler discovery

Add the new handler type in `<Module>Module.cs`:

```csharp
public static WolverineOptions AddOrdersHandlers(this WolverineOptions opts)
{
    opts.Discovery.IncludeType<CancelOrderHandler>();
    return opts;
}
```

If you added an integration subscriber or publisher handler, include that type too.

### Endpoint mapping

Add the endpoint mapping in `<Module>Module.cs`:

```csharp
public static IEndpointRouteBuilder MapOrdersEndpoints(this IEndpointRouteBuilder app)
{
    CancelOrderEndpoint.Map(app);
    return app;
}
```

## Testing rules

Every new slice needs integration coverage.

Write the test in:

```text
tests/Modules/<Module>/Modulith.Modules.<Module>.IntegrationTests/Features/<FeatureName>Tests.cs
```

Integration test requirements:

- real Postgres via Testcontainers
- real Wolverine pipeline
- authenticated client via `AuthenticatedClientBuilder` when auth is required
- happy path coverage
- at least the common failure path for the slice

If the slice publishes or triggers messages:

- use Wolverine `TrackActivity()`
- assert the message flow and resulting side effects

Do not write handler unit tests. Handler behavior belongs in integration tests.

Write unit tests only for pure domain behavior:

- aggregate factories
- aggregate state transitions
- value objects
- domain event emission

## Common mistakes

Avoid these:

- putting business logic branches in the handler instead of the aggregate
- using another module's internal project or DbContext
- forgetting to register the handler in `<Module>Module.cs`
- forgetting to map the endpoint in `<Module>Module.cs`
- leaving route strings inline in the endpoint
- using domain types in requests, responses, or public events
- making Wolverine handlers `internal`
- skipping the integration test
- editing committed migrations instead of adding a new one
- changing `Api/Program.cs` or `src/AppHost/AppHost.cs` for a normal slice

## Definition of done

A slice is done when all of these are true:

- the feature folder contains the expected files for the slice type
- routes are defined in `<Module>Routes.cs`
- handler types are registered in `<Module>Module.cs`
- endpoint mapping is registered in `<Module>Module.cs`
- expected failures return module errors, not inline strings or exceptions
- the integration test passes
- architectural tests still pass

## Reference material

Use these as the source of truth:

- `docs/how-to/add-a-slice.md`
- `docs/examples/command-with-event.md`
- `docs/examples/simple-query-slice.md`
- `docs/how-to/cross-module-events.md`
- `docs/how-to/handle-failures.md`
- `docs/testing-strategy.md`
- `/CLAUDE.md`
- `/src/Modules/CLAUDE.md`