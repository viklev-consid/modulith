# How-to: Handle Failures

Modulith uses the Result pattern for expected failures and exceptions for truly exceptional cases. This guide walks through the distinction and the practical conventions.

For the reasoning, see [`../adr/0004-result-pattern.md`](../adr/0004-result-pattern.md) and [`../adr/0025-problem-details-for-errors.md`](../adr/0025-problem-details-for-errors.md).

---

## The rule

| Failure kind | Mechanism | Why |
|---|---|---|
| Validation failure | `Result.Fail(Error.Validation(...))` | Expected; client input may be wrong |
| Entity not found | `Result.Fail(Error.NotFound(...))` | Expected; user may request missing resource |
| Business rule violation | `Result.Fail(Error.Conflict(...))` | Expected; state may be incompatible with operation |
| Authorization failure | `Result.Fail(Error.Forbidden(...))` | Expected; user may lack permission |
| Concurrency conflict | `Result.Fail(Error.Conflict(...))` (after catching `DbUpdateConcurrencyException`) | Expected; two writers raced |
| Bug (null, out-of-range, unexpected state) | Throw | Exceptional; should not happen |
| Infrastructure fault (DB dead, network) | Throw | Exceptional; often transient, sometimes fatal |
| External API error | Depends. If expected (provider returned 4xx with a documented code): Result. If unexpected (5xx, timeout): Throw. | See below |

---

## Using `ErrorOr`

The template uses `ErrorOr<T>` (see [`adr/0004-result-pattern.md`](../adr/0004-result-pattern.md)).

### Success

```csharp
public async Task<ErrorOr<OrderDto>> Handle(...)
{
    var order = ...;
    return new OrderDto(...);   // implicit conversion from T to ErrorOr<T>
}
```

### Failure

```csharp
if (order is null)
    return Errors.Orders.NotFound(orderId);

if (order.Status == OrderStatus.Shipped)
    return Errors.Orders.CannotCancelShipped;
```

### Module-local error catalog

Each module defines a static `Errors` class with all of its error constants:

```csharp
// src/Modules/Orders/Modulith.Modules.Orders/Domain/Errors.cs
namespace Modulith.Modules.Orders.Domain;

internal static class Errors
{
    public static class Orders
    {
        public static Error NotFound(OrderId id) =>
            Error.NotFound("orders.not_found", $"Order {id.Value} was not found.");

        public static readonly Error CannotCancelShipped =
            Error.Conflict("orders.cannot_cancel_shipped", "A shipped order cannot be cancelled.");

        public static readonly Error EmptyItems =
            Error.Validation("orders.empty_items", "Order must contain at least one item.");
    }
}
```

Stable error codes (`orders.not_found`) are the programmatic contract. Clients pattern-match on them.

---

## Mapping Result to HTTP

The endpoint maps `ErrorOr<T>` to an HTTP response via a shared extension:

```csharp
app.MapPost("/orders", async (Request req, IMessageBus bus) =>
{
    var result = await bus.InvokeAsync<ErrorOr<Response>>(req.ToCommand());
    return result.ToProblemDetailsOr(value => Results.Created($"/orders/{value.Id}", value));
});
```

The extension inspects each `Error`'s type:

| `Error.Type` | HTTP status |
|---|---|
| `ErrorType.Validation` | 422 (with `ValidationProblemDetails`) |
| `ErrorType.NotFound` | 404 |
| `ErrorType.Conflict` | 409 |
| `ErrorType.Unauthorized` | 401 |
| `ErrorType.Forbidden` | 403 |
| `ErrorType.Unexpected` | 500 |

Clients receive consistent `ProblemDetails` with `errorCode` for programmatic handling.

---

## Catching exceptions in handlers

Sometimes an expected failure manifests as an exception (e.g., EF Core concurrency conflicts). Catch and convert:

```csharp
public async Task<ErrorOr<Response>> Handle(Command cmd, CancellationToken ct)
{
    try
    {
        // ... normal flow
        await _db.SaveChangesAsync(ct);
        return new Response(...);
    }
    catch (DbUpdateConcurrencyException)
    {
        return Errors.Orders.ConcurrencyConflict;
    }
}
```

Don't catch broad `Exception`. Catch specific types that represent specific expected failures.

---

## The global exception handler

Anything that escapes a handler is caught by `IExceptionHandler`. It:

1. Logs the exception with full detail (including trace ID).
2. Returns a **sanitized** 500 ProblemDetails:

```json
{
  "type": "https://docs.modulith.dev/errors/internal_error",
  "title": "An error occurred processing your request.",
  "status": 500,
  "detail": "An unexpected error occurred. Reference: <traceId>",
  "traceId": "<traceId>"
}
```

Critical: **never include the exception message or stack trace in the response body.** Attackers use those.

---

## Domain invariants as Results

Aggregate methods return `Result`:

```csharp
public Result Cancel(string reason)
{
    if (Status == OrderStatus.Shipped)
        return Result.Fail("Cannot cancel a shipped order.");

    Status = OrderStatus.Cancelled;
    CancelledAt = DateTimeOffset.UtcNow;
    RaiseEvent(new OrderCancelled(Id, reason));
    return Result.Ok();
}
```

Factory methods return `Result<TSelf>`:

```csharp
public static Result<Order> Create(CustomerId customerId)
{
    if (customerId is null)
        return Result.Fail<Order>("Customer is required.");
    return new Order(OrderId.New(), customerId);
}
```

In the handler:

```csharp
var cancel = order.Cancel(cmd.Reason);
if (cancel.IsFailed)
    return Error.Conflict("orders.cannot_cancel", cancel.Errors[0].Message);
```

(Or a helper that does this conversion in one step.)

---

## External API errors

Three patterns:

### Expected, documented provider errors

The provider returns a 4xx with a documented error code. Treat as Result:

```csharp
var result = await _paymentProvider.ChargeAsync(...);
if (result.IsDeclined)
    return Errors.Payments.Declined(result.DeclineReason);
```

### Unexpected provider errors

The provider returns 5xx or times out. These are infrastructure faults:

- For retryable operations, configure Polly (via `ServiceDefaults` HTTP resilience) and let retries handle it.
- For non-retryable, let the exception propagate. Global exception handler returns 500.

### Hybrid: provider returned 5xx but we don't want the client to see 500

Catch the exception, log it, and return `Error.Unexpected("payments.provider_unavailable", ...)`. The handler returns 503. Client sees a deliberate failure, operator sees the underlying cause in logs.

---

## Validation in two layers

**Request-level** (FluentValidation):

```csharp
public sealed class PlaceOrderValidator : AbstractValidator<PlaceOrderRequest>
{
    public PlaceOrderValidator()
    {
        RuleFor(x => x.Items).NotEmpty().WithErrorCode("orders.empty_items");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Sku).NotEmpty();
            item.RuleFor(i => i.Quantity).GreaterThan(0);
        });
    }
}
```

Runs before the handler. Failures return 422 `ValidationProblemDetails`.

**Domain-level** (aggregate methods):

```csharp
public Result AddLine(Sku sku, int quantity)
{
    if (Status != OrderStatus.Draft)
        return Result.Fail("Cannot modify a non-draft order.");
    // ...
}
```

Request validation catches shape/format errors. Domain validation catches state/rule violations. **Both are needed.**

---

## Common mistakes

- **Throwing for validation failures.** The endpoint won't map it to 422 — it becomes a 500 via the global handler. Return an `Error.Validation`.
- **Swallowing exceptions silently.** If you catch and continue without logging or converting to a Result, bugs hide indefinitely.
- **Broad `catch (Exception)`.** Masks bugs. Catch specific types.
- **Leaking exception messages to clients.** Always go through the global exception handler for unexpected errors.
- **Mixing Result and throw in the same method.** Pick one style per operation.
- **Error codes that aren't stable.** If a code changes, clients break. Treat the error catalog like a public API.

---

## Related

- [`../adr/0004-result-pattern.md`](../adr/0004-result-pattern.md)
- [`../adr/0008-fluent-validation.md`](../adr/0008-fluent-validation.md)
- [`../adr/0025-problem-details-for-errors.md`](../adr/0025-problem-details-for-errors.md)
