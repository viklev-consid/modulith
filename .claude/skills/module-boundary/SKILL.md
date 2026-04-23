---
name: module-boundary
description: Decision guide for cross-module communication in Modulith. Covers Contracts projects, choosing events vs queries vs commands, eventual consistency, and common boundary violations.
---

# Module Boundary

Use this skill when a change crosses module boundaries or might cross them soon.

Typical triggers:

- one module needs data owned by another module
- one module needs to react when another module changes state
- you are deciding whether to add something to a `.Contracts` project
- you are unsure whether the right shape is an event, a query, or a command

Do not use this skill when:

- the change stays entirely inside one module
- the task is only adding a feature slice after the boundary decision is already clear
- the task is only domain modeling under `Domain/`

## Read first

Before changing code, read:

1. `/CLAUDE.md`
2. `/src/Modules/CLAUDE.md`
3. `/src/Modules/<Module>/CLAUDE.md` if it exists
4. `docs/how-to/cross-module-events.md`
5. `docs/adr/0005-module-communication-patterns.md`
6. `docs/adr/0006-internal-vs-public-events.md`
7. `docs/adr/0023-per-module-dbcontext.md`

## Non-negotiables

The boundary rules are structural, not stylistic.

- every module has an internal project and a `.Contracts` project
- other modules may reference only the `.Contracts` project
- a module may never reference another module's internal project
- modules do not share DbContexts
- modules do not read or write each other's tables directly
- there are no cross-schema foreign keys between modules

If your design requires any of those, stop. The design is wrong or needs an explicit architecture decision.

## Allowed dependency directions

These are the allowed reference patterns:

- `Api` may reference modules' internal projects for composition
- `Modulith.Modules.<Name>` may reference its own `.Contracts` project
- `Modulith.Modules.<Name>` may reference other modules' `.Contracts` projects
- `.Contracts` projects may reference only shared contract-level packages and shared kernel abstractions already used by the repo

These are forbidden:

- one module referencing another module's internal project
- a `.Contracts` project referencing its own internal project
- a `.Contracts` project chaining to another module's `.Contracts` project

## Choose the communication pattern

Use this decision order.

### 1. Integration event

This is the default and preferred pattern.

Choose an integration event when:

- module A did something and module B may react
- the publisher should not need an immediate answer
- eventual consistency is acceptable
- the receiver is conceptually a subscriber, not a dependency

Example shape:

- module A raises an internal domain event in its aggregate
- an internal handler maps that to a public `V1` integration event in `.Contracts/Events`
- module B subscribes in its `Integration/` folder

### 2. Query

Use a cross-module query when module A needs a synchronous read owned by module B.

Choose a query when:

- the caller genuinely needs data now to complete its own flow
- the data belongs to another module and should stay there
- returning a DTO is enough

Keep queries narrow. If a query starts returning a large internal shape or many unrelated fields, the boundary is drifting.

### 3. Command

Cross-module commands are allowed but rare.

Use a command only when:

- module A is legitimately telling module B to do work B owns
- an event would be too indirect or too delayed for the use case
- the imperative relationship is part of the business design, not just convenience

Treat cross-module commands as a smell to justify, not a default.

## Event design rules

When publishing something outside the module:

- define the public event in `<Module>.Contracts/Events`
- suffix it with a version such as `UserEmailChangedV1`
- use primitives and DTOs only, never domain types
- include only what subscribers need
- include an occurrence timestamp when subscriber ordering or audit context matters

Internal domain events and public integration events are separate types.

Do not publish a `Domain/Events/*` type directly outside the module.

## Query design rules

When adding a cross-module query:

- define the query record in `.Contracts/Queries`
- define any shared DTOs in `.Contracts/Dtos` if needed
- dispatch it through `IMessageBus.InvokeAsync<T>()`
- return only the data the caller needs

If the caller wants to join multiple modules' data into one screen, that is fine. Do the composition in the caller or API layer, not with cross-schema joins.

## Command design rules

When adding a cross-module command:

- define the command in `.Contracts/Commands`
- keep it explicit and business-oriented
- make the receiving module own validation and state transitions
- avoid command chains where one module immediately commands another and back again

If the design starts to look like synchronous orchestration across many modules, reconsider the boundaries.

## Eventual consistency rules

Integration events are eventually consistent by design.

Design implications:

- subscribers may process later, not inline with the publisher's HTTP response
- subscribers may see the same message more than once
- publisher flows must not depend on subscriber side effects being complete immediately

If module A must know the answer before returning, use a query or keep the logic inside one module.

If the publisher's correctness depends on subscriber completion, the boundary is probably wrong.

## Idempotency rules for subscribers

Subscribers must assume at-least-once delivery.

Prefer:

- state-based updates
- existence checks before inserts
- unique constraints for dedup where needed
- silent no-op behavior when the desired state is already true

Do not assume the outbox makes consumers idempotent. It does not.

## Common boundary smells

Treat these as warnings that the design needs correction:

- referencing another module's internal namespace
- adding a foreign key to another module's table
- injecting another module's service directly
- reading another module's DbContext from a handler
- publishing internal domain event types as public contracts
- using a cross-module command where a subscriber event would do
- adding large contract DTOs that mirror internal EF entities
- event loops where A publishes to B and B publishes back to A for the same workflow

## Worked decision heuristics

Use these shortcuts.

- "Something happened, react if you care" -> integration event
- "I need your data now to finish my flow" -> query
- "You own this action, do it now" -> command, but justify it
- "I need another module's table" -> wrong design
- "I need another module's internal service" -> wrong design

## Definition of done

A cross-module change is correctly designed when:

- no internal project reference crosses a module boundary
- any new public message lives in the correct `.Contracts` folder
- integration events are versioned and use primitives
- subscribers live in `Integration/` and are `public`
- synchronous reads use queries, not direct DB access
- architecture tests still pass
- integration tests cover the new flow if messages are involved

## Reference material

Use these as the source of truth:

- `docs/how-to/cross-module-events.md`
- `docs/adr/0005-module-communication-patterns.md`
- `docs/adr/0006-internal-vs-public-events.md`
- `docs/adr/0003-wolverine-for-messaging.md`
- `/CLAUDE.md`
- `/src/Modules/CLAUDE.md`