# ADR-0015: Architectural Tests for Boundary Enforcement

## Status

Accepted

## Context

A modular monolith without enforcement is not a modular monolith — it's a traditional monolith where someone tried to use folders for discipline. The rules in this template (ADR-0005, 0009, 0023, etc.) are only as real as their enforcement.

Enforcement options:

1. **Documentation only.** Rules in docs, trust developers. Fails on real teams past a quarter.
2. **Code review only.** Humans catch violations. Fails at scale and is inconsistent.
3. **Project references.** Prevent references at compile time. Good, but doesn't catch everything (namespace-level rules, naming conventions, attribute use).
4. **Architectural tests.** Compiled-assembly inspection in the test suite. Catches what project references can't.

The combination of project references + architectural tests catches essentially every structural mistake at the fastest possible feedback loop.

## Decision

A dedicated test project `Modulith.Architecture.Tests` runs a comprehensive set of boundary and convention rules as part of the fast CI tier. Uses **NetArchTest** for its readable API.

### Enforced rules

**Module boundary rules:**

- A module's internal assembly must not be referenced by any other module's internal assembly. Only `Api` may reference internal modules.
- A module's `.Contracts` assembly must not reference its own internal assembly.
- A module's `.Contracts` assembly must not reference another module's `.Contracts` assembly.

**Domain purity rules:**

- Types in `*.Domain.*` namespaces must not depend on `Microsoft.EntityFrameworkCore.*`.
- Types in `*.Domain.*` must not depend on `Microsoft.AspNetCore.*`.
- Types in `*.Domain.*` must not depend on `Wolverine.*`.
- Types in `*.Domain.*` must not depend on `FluentValidation.*`.
- Types in `*.Domain.*` must not depend on `Microsoft.FeatureManagement.*`.
- Types in `*.Domain.*` must not depend on `Microsoft.Extensions.Caching.*`.

**Entity rules:**

- Classes inheriting `AggregateRoot<>` or `Entity<>` must not have any public settable properties.
- Classes inheriting `AggregateRoot<>` or `Entity<>` must have no public constructors (they use factory methods).

**Slice rules:**

- Types ending in `Handler`, `Validator`, `Endpoint` must live under a `Features/*/` folder (in their namespace).
- Request and Response types must be `sealed record` types.
- Command and Query types must be `sealed record` types.

**Configuration rules:**

- `Microsoft.Extensions.Configuration.IConfiguration` must only be injected in types whose name ends in `Module` (i.e., the registration extensions).

**Feature management rules:**

- `Microsoft.FeatureManagement.IFeatureManager` must not be depended upon by types in `*.Domain.*`.

**GDPR rules:**

- Modules with entities carrying a `UserId` property must contain a type implementing `IPersonalDataEraser`, OR be marked with `[NoPersonalData]` at the assembly level.

**Event rules:**

- Types inheriting `DomainEvent` must live under `*.Domain.Events.*` namespaces.
- Integration event types (in `*.Contracts.Events.*`) must be `sealed record` and must end with a version suffix (`V1`, `V2`, ...).

**Shared kernel rules:**

- `Modulith.Shared.Kernel` must depend only on the BCL — no other project references, no third-party packages except the Result library.

**Notification rules:**

- Types must not inject `IEmailSender` or `ISmsSender` except inside `Modulith.Modules.Notifications` and `Modulith.Shared.Infrastructure`.

**Blob storage rules:**

- Types must not call `System.IO.File` methods on user-content paths. (Approximated by forbidding `System.IO.File` usage outside allowlisted files.)

### Failure messages

Every rule has a custom failure message that names the rule, the offending type(s), and the suggested fix. Example:

> FAIL: Modulith.Modules.Orders.Persistence.OrdersDbContext depends on Modulith.Modules.Users.Persistence.UsersDbContext. Modules must not share DbContexts. Use Users' public Contracts (via IMessageBus) to request data. See ADR-0005 and ADR-0023.

This matters most for agents: a failing test that says "rule X violated" wastes cycles; a failing test that says "move type Y to folder Z" saves them.

## Consequences

**Positive:**

- Boundary violations fail the build within a minute.
- Architectural rules are executable documentation — you can't drift from them without the test going red.
- New contributors (and agents) learn the rules by breaking them and reading the messages.
- Refactors that would have silently broken the architecture are caught.

**Negative:**

- Adding a new rule requires a test, and some rules are fiddly to express in NetArchTest.
- Assembly scanning has startup cost; the test project is fast but not instantaneous.
- Rules need to evolve with the codebase. A rule that was right at v1 may be wrong at v3. Changing a rule requires an ADR update.
- Over-strict rules create friction. The rules in this template have been chosen to catch real mistakes, not to impose style preferences.

## Related

- ADR-0005 (Module Communication): the reference rules enforced here.
- ADR-0009 (Rich Domain Model): no-public-setter rule.
- ADR-0021 (Config and Secrets): IConfiguration-in-registration-only rule.
- ADR-0027 (Agentic Development): actionable failure messages serve agents.
