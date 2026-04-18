# ADR-0005: Module Communication via Contracts Projects

## Status

Accepted

## Context

The modular monolith's value comes from enforced module boundaries. Without them, "modules" are just folders and the codebase degrades into a traditional monolith under load. The question is: *how* do modules communicate?

Options:

1. **Direct service calls** — Module A injects Module B's service. Simplest, but couples modules at the type level.
2. **Shared database** — Modules read each other's tables. Hard to enforce, hard to extract later.
3. **Public interfaces** — Each module exposes interfaces others depend on. Better, but dependencies are still compile-time.
4. **Message-based** — Modules publish events and send commands/queries through a bus. The sender doesn't know the receiver exists at compile time.

Pure message-based is the most decoupled but hardest to navigate. Pure interface-based is easier to navigate but tightly coupled. A hybrid is usually the right answer.

## Decision

Each module has two projects:

- `Modulith.Modules.<Name>` — internal. Contains domain, persistence, handlers, endpoints, seeders.
- `Modulith.Modules.<Name>.Contracts` — public. Contains records for commands, queries, and integration events that other modules may depend on.

**Allowed references:**

- `Api` may reference every module's internal project (for composition).
- A module's internal project may reference its own `.Contracts` project.
- A module's internal project may reference other modules' `.Contracts` projects (for subscribing or invoking).
- A `.Contracts` project may reference only `Shared.Kernel` and `Shared.Contracts`.

**Forbidden references (enforced by architectural tests):**

- A module's internal project referenced by any other module.
- A `.Contracts` project referencing its own internal project.
- A `.Contracts` project referencing another module's `.Contracts` project (avoid transitive contract chains).

**Three communication patterns:**

1. **Integration events** (most common, preferred). Module A publishes `OrderPlaced` from `Orders.Contracts`. Module B subscribes with a handler in its `Integration/` folder. Delivered via Wolverine's outbox.

2. **Queries** (for synchronous reads across modules). Module A exposes `GetUserById` as a query in `Users.Contracts`. Module B sends via `IMessageBus.InvokeAsync<UserDto>(query)`.

3. **Commands** (rare, for legitimate cross-module commands). Module A exposes `DeactivateUser` in `Users.Contracts`. Module B sends it. Usually a sign the boundary is wrong — prefer events.

## Consequences

**Positive:**

- Boundary violations fail the build or the arch tests. Enforcement is automatic.
- Extracting a module to a separate service is mechanical: the `.Contracts` project becomes the wire contract; the transport changes from in-process to a broker.
- Navigation is reasonable — contracts are explicit and discoverable.
- Refactoring within a module is free. Public surface is the `.Contracts` project only.

**Negative:**

- Two projects per module. Slight solution clutter.
- Contract changes are breaking changes. Intentional, but requires discipline — bumping a field means bumping subscribers.
- New contributors may try to reference internal code. The arch test failure messages are written to redirect them.
- Integration events are eventually consistent. Developers accustomed to synchronous control flow may need to adjust.

## Related

- ADR-0003 (Wolverine): provides the message bus and outbox.
- ADR-0006 (Internal vs Public Events): the distinction between in-module domain events and cross-module integration events.
- ADR-0015 (Architectural Tests): how the rules above are enforced.
- ADR-0023 (DbContext Per Module): the database-level counterpart to these project rules.
