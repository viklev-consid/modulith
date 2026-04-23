---
name: rich-domain-model
description: Patterns for modeling aggregates, value objects, typed IDs, and internal domain events in Modulith Domain folders.
---

# Rich Domain Model

Use this skill when you are creating or refactoring code under `src/Modules/<Module>/Modulith.Modules.<Module>/Domain/`.

Typical triggers:

- adding a new aggregate root
- creating or changing a value object
- deciding where a business invariant belongs
- moving logic out of handlers and into the model

Do not use this skill when:

- the task is primarily endpoint or handler wiring
- the task is primarily EF Core configuration or migrations
- the change is about public integration contracts, not internal domain behavior

## Read first

Before changing domain code, read:

1. `/CLAUDE.md`
2. `/src/Modules/CLAUDE.md`
3. `/src/Modules/<Module>/CLAUDE.md` if it exists
4. `docs/adr/0009-rich-domain-model.md`
5. `docs/adr/0004-result-pattern.md`
6. one nearby aggregate and one nearby value object in the same module

## Domain purity rules

Everything under `Domain/` is pure domain code.

Forbidden dependencies in `Domain/` include:

- EF Core
- ASP.NET Core
- Wolverine
- FluentValidation
- HybridCache
- feature flags
- logging, HTTP, or infrastructure concerns

If a domain type needs those, the design is wrong.

## Live result convention

The current codebase uses `ErrorOr` for expected domain failures.

Follow the live code:

- aggregate factory methods return `ErrorOr<TAggregate>`
- state transitions return `ErrorOr<Success>` when they can fail
- expected failures return module errors
- unexpected bugs still throw

Do not introduce a second competing success or failure abstraction inside the same module.

## Aggregate rules

Aggregates should look like this structurally:

- `public sealed class <Entity> : AggregateRoot<<IdType>>`
- private constructor for real creation
- private parameterless constructor only for EF materialization when needed
- public properties with private setters
- public behavior methods for state transitions
- internal state changes only through those methods

Typical aggregate responsibilities:

- enforce invariants
- own lifecycle transitions
- raise internal domain events
- protect consistency across its own data

Aggregates should not:

- query the database
- call external services
- know about HTTP or transport concerns
- know about public integration event versioning

## Factory method rules

Creation goes through a factory method, usually `Create(...)`.

Factory methods should:

- validate invariant-level inputs
- normalize values when appropriate
- return a module error on expected failure
- construct a valid aggregate in one step
- raise an internal domain event if creation is business-significant

Example shape:

```csharp
public static ErrorOr<Product> Create(Sku sku, string name, Money price)
{
    if (string.IsNullOrWhiteSpace(name))
    {
        return CatalogErrors.ProductNameEmpty;
    }

    var product = new Product(ProductId.New(), sku, name.Trim(), price);
    product.RaiseEvent(new ProductCreated(product.Id, sku.Value, product.Name));
    return product;
}
```

## State transition rules

Business actions are methods on the aggregate, not property assignments in handlers.

Prefer:

- `order.Cancel(reason)`
- `user.ChangeEmail(newEmail)`
- `product.UpdatePrice(newPrice)`

Avoid:

- `order.Status = Cancelled`
- `user.Email = newEmail`
- `product.Price = newPrice` from outside the aggregate

State transitions should:

- enforce rules before mutation
- mutate all related fields together
- raise internal domain events when something meaningful happened
- return `ErrorOr<Success>` when they can fail

## Value object rules

Use value objects for non-trivial primitives.

Good candidates:

- email
- money
- SKU
- role
- date ranges or structured identifiers

Value object guidelines:

- prefer `sealed record` when nearby code follows that shape
- keep them immutable
- expose a `Create(...)` factory that validates and normalizes
- keep equality by value
- keep parsing and invariant logic local to the type

Do not leak validation rules for a value object into handlers or validators.

## Typed ID rules

Use typed IDs inside the domain.

- `UserId`, not `Guid`
- `OrderId`, not `Guid`
- `ProductId`, not `Guid`

Map raw `Guid` values at the module boundary, usually in the endpoint or command construction.

Do not pass raw `Guid` values around inside the domain when a typed ID already exists.

## Domain event rules

Internal domain events belong under `Domain/Events/`.

Use them when:

- the aggregate has completed a business-significant change
- other internal handlers in the same module may react
- the change may later be mapped to a public integration event

Rules:

- raise internal events from the aggregate with `RaiseEvent(...)`
- keep them internal to the module's domain model
- do not design them around serialization concerns
- do not publish them directly across module boundaries

Public integration events are separate types handled elsewhere.

## Error rules

Expected domain failures should come from the module error catalog.

- keep module errors in `Errors/<Module>Errors.cs`
- reuse those errors from aggregates and value objects
- do not inline user-visible or API-visible error strings in domain methods

## Handler versus aggregate split

Use this split.

The handler does:

- database lookups
- uniqueness checks that require I/O
- orchestration across aggregates
- saving changes
- publishing integration events

The aggregate does:

- invariant validation
- state transitions
- internal domain event emission

If a handler contains many `if` branches about business state, move that logic into the aggregate.

## Auditing and persistence-aware interfaces

It is acceptable for a domain entity to implement shared kernel abstractions already used by the repo, such as auditing interfaces.

It is not acceptable to pull infrastructure packages into the domain to do that work.

## Common mistakes

Avoid these:

- public setters on entity state
- constructors that let callers build invalid entities directly
- domain methods returning booleans instead of meaningful success or error results
- handlers mutating entity properties directly
- value object validation duplicated in validators and handlers
- domain events designed like public integration contracts
- catching infrastructure exceptions inside the domain

## Definition of done

A domain-modeling change is complete when:

- `Domain/` contains no infrastructure dependencies
- aggregates expose behavior methods rather than public mutation
- factory methods and transitions return `ErrorOr` results for expected failures
- value objects own their validation
- internal domain events are raised from aggregates when appropriate
- handlers only orchestrate and persist
- unit tests cover the changed invariants and state transitions

## Reference material

Use these as the source of truth:

- `docs/adr/0009-rich-domain-model.md`
- `docs/adr/0004-result-pattern.md`
- `/CLAUDE.md`
- `/src/Modules/CLAUDE.md`
- nearby domain types such as `src/Modules/Catalog/Modulith.Modules.Catalog/Domain/Product.cs`
