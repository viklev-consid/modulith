# ADR-0006: Separate Internal Domain Events from Public Integration Events

## Status

Accepted

## Context

Domain-Driven Design includes the concept of *domain events* — things that happen within an aggregate that are worth notifying interested parties about. The temptation is to publish these events directly to other modules.

Doing so couples the domain model of one module to every subscriber's expectations. If Users' `User` aggregate raises `UserEmailChanged(OldEmail, NewEmail)` internally and that event is also the integration contract, then:

- Renaming the event breaks every subscriber.
- Adding a field that internal handlers need breaks the wire format.
- Serialization concerns (null handling, enum values) leak into domain code.
- Subscribers can infer internal structure from the event shape.

The alternative: separate types. An internal event is raised inside the aggregate. An outbound integration event is a distinct type, published by an internal handler that maps from the internal event.

## Decision

Each module has two event tiers:

1. **Internal domain events** — live in the module's `Domain/Events/` folder. Raised by aggregates. Not part of any public contract. May change freely.
2. **Public integration events** — live in the module's `.Contracts/Events/` folder. Published via the outbox to other modules. Part of the module's public contract. Changes are breaking.

Internal handlers inside the module subscribe to internal events and publish the corresponding public events. This mapping step is where serialization concerns, naming, and versioning are handled.

Not every internal event becomes a public event. Some stay private. That's normal and expected.

## Consequences

**Positive:**

- Internal refactoring does not break external subscribers.
- Public event shapes are designed for consumers, not derived from domain structure.
- Versioning is possible — a module can publish `v1` and `v2` of an integration event concurrently during a migration.
- Testing is clearer: internal behavior tested via domain events, cross-module flows tested via integration events.

**Negative:**

- Two event types per public event. More code.
- Occasional mapper boilerplate. Mitigated by records and object initializers — usually a few lines.
- Easy to forget to publish an integration event. Caught by integration tests that verify the outbox contents.

## Example

```
// Internal — may change freely
namespace Modulith.Modules.Users.Domain.Events;
public sealed record UserEmailChanged(UserId UserId, string OldEmail, string NewEmail);

// Public — part of the contract
namespace Modulith.Modules.Users.Contracts.Events;
public sealed record UserEmailChangedV1(Guid UserId, string NewEmail, DateTimeOffset OccurredAt);

// Internal handler that maps one to the other
internal sealed class PublishUserEmailChangedHandler
{
    public async Task Handle(UserEmailChanged @event, IMessageBus bus, CancellationToken ct) =>
        await bus.PublishAsync(new UserEmailChangedV1(@event.UserId.Value, @event.NewEmail, DateTimeOffset.UtcNow));
}
```

Note the differences: public event uses `Guid` (not the strongly-typed `UserId`), omits `OldEmail` (subscribers don't need it), includes `OccurredAt` (for ordering in eventually consistent consumers), and is versioned.

## Related

- ADR-0003 (Wolverine): the outbox publishes integration events.
- ADR-0005 (Module Communication): integration events are one of the three cross-module patterns.
