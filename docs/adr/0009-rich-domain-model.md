# ADR-0009: Rich Domain Model with Private Setters

## Status

Accepted

## Context

Two styles dominate .NET domain modeling:

1. **Anemic** — entities are data bags with public get/set properties. All behavior lives in service classes that read and mutate them.
2. **Rich** — entities enforce their own invariants. State changes go through methods that validate rules, update state atomically, and raise domain events. Properties have private (or protected) setters; no direct mutation from outside.

Anemic models are simpler to reason about for small systems, but they externalize invariants into services, which means invariants are easy to bypass. The bigger the domain, the more the anemic style fails — complex rules spread across services with no single source of truth.

Rich models require more discipline but produce code where invariants are locally provable. A line like `order.Cancel()` either succeeds (and the order is cancelled) or fails with a specific reason — there's no path where `order.Status = Cancelled` bypasses the check.

Vertical slicing (ADR-0002) has a known risk: the handler sits next to the entity, and it's tempting to write procedural code that mutates entity properties directly. A rich domain model is the structural discipline that prevents this.

## Decision

Aggregates and entities follow these rules:

1. **No public setters.** Properties are either `get; private set;` or (preferably) init-only with explicit state transitions via methods.
2. **Constructors are private or internal.** Object creation goes through factory methods, usually `public static Result<T> Create(...)`.
3. **State transitions are methods, not property assignments.** `order.Cancel(reason)`, `user.ChangeEmail(newEmail)`. These return `Result` and enforce invariants.
4. **Domain events are raised from within the aggregate.** Aggregates inherit from an `AggregateRoot` base that tracks uncommitted events. Wolverine middleware picks them up post-save and dispatches internal handlers.
5. **Strongly-typed IDs.** `UserId`, `OrderId`, etc. — not raw `Guid`. Prevents accidentally passing a `UserId` where an `OrderId` was expected.
6. **Value objects for non-trivial primitives.** `Email`, `Money`, `DateRange` — types that own their validation and comparison.

Architectural tests enforce points 1 (no public setters) and 2 (private constructors). The rest are code-review conventions.

## Example shape

```csharp
public sealed class Order : AggregateRoot<OrderId>
{
    public OrderStatus Status { get; private set; }
    public Money Total { get; private set; }
    private readonly List<OrderLine> _lines = new();
    public IReadOnlyCollection<OrderLine> Lines => _lines;

    private Order(OrderId id, CustomerId customerId) : base(id)
    {
        CustomerId = customerId;
        Status = OrderStatus.Draft;
        Total = Money.Zero;
    }

    public static Result<Order> Create(CustomerId customerId)
    {
        if (customerId is null) return Result.Fail<Order>("Customer is required.");
        return new Order(OrderId.New(), customerId);
    }

    public Result Cancel(string reason)
    {
        if (Status == OrderStatus.Shipped)
            return Result.Fail("Cannot cancel a shipped order.");
        Status = OrderStatus.Cancelled;
        RaiseEvent(new OrderCancelled(Id, reason));
        return Result.Ok();
    }
}
```

## Consequences

**Positive:**

- Invariants are local and provable. `order.Cancel()` is the only way to cancel an order; if that method is right, cancellation is right.
- Unit tests are trivial — construct an aggregate, call methods, assert state and events.
- EF Core accommodates private setters via configuration. Backing fields are well-supported.
- Refactoring is safer — changing internal state structure doesn't break callers, because callers can't access it.

**Negative:**

- More code than the anemic style. Accepted.
- EF Core configuration is slightly more involved — `.UsePropertyAccessMode(PropertyAccessMode.Field)`, field-backed navigation properties. Documented.
- Serialization of aggregates requires thought. Solved by not serializing aggregates directly — DTOs at the boundary.
- `AutoFixture` doesn't work cleanly with private setters and factory methods. We use object mothers instead (see `Modulith.TestSupport`).

## Related

- ADR-0002 (Vertical Slices): rich domain is the discipline that prevents slice drift into anemia.
- ADR-0004 (Result Pattern): factory methods and state transitions return `Result`.
- ADR-0015 (Architectural Tests): enforces no-public-setters rule.
- ADR-0023 (DbContext Per Module): each module's DbContext configures its own aggregates.
