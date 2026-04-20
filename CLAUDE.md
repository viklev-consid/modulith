# CLAUDE.md — Modulith Agent Operating Manual

This file is the primary operating manual for AI coding agents working in the Modulith codebase. Read it before making changes. It encodes non-obvious constraints and conventions that will otherwise cost cycles to re-derive.

Scoped `CLAUDE.md` files exist in subdirectories (`src/Modules/`, `tests/`, individual modules). When working in those areas, read those too.

---

## What this codebase is

A modular monolith RESTful API in .NET 10 / C# 14, orchestrated by .NET Aspire 13.x. Each module is a vertical slice of business capability with its own domain, persistence, and endpoints. Modules communicate internally via Wolverine (in-process messaging + outbox + background jobs). Cross-module communication is strictly through public `.Contracts` projects — never across internal boundaries.

If you want the full picture, read `docs/architecture.md`. If you want the reasoning behind any specific decision, check `docs/adr/`.

---

## Invariants

These are constraints that architectural tests enforce. They are not suggestions.

1. **Modules communicate only through `.Contracts` projects.** A module may reference another module's `.Contracts` project to publish or subscribe to its public messages. A module may never reference another module's internal project.
2. **Domain has no infrastructure dependencies.** Code under any `Domain/` folder must not reference EF Core, ASP.NET Core, Wolverine, FluentValidation, HybridCache, FeatureManagement, or any other infrastructure concern. Domain is pure C#.
3. **Entities have no public setters.** State transitions happen through methods on aggregates. Factory methods (usually `Create`) return `Result<T>` and validate invariants.
4. **Commands return `Result<T>`, not exceptions, for expected failures.** Exceptions are for bugs and infrastructure faults. Validation failures, missing entities, and business rule violations all return `Result`.
5. **Endpoints depend only on `IMessageBus`.** Endpoints do not depend on handlers, repositories, or domain services directly. They translate HTTP requests to commands and dispatch them.
6. **Raw `IConfiguration` is not injected outside registration code.** All configuration access is through strongly-typed `IOptions<T>` with `ValidateOnStart()`.
7. **`IFeatureManager` is not used in `Domain/` folders.** Feature flags live at the edges — endpoint routing, handler selection, infrastructure composition.
8. **Each module owns its own `DbContext` and its own schema.** Migrations are module-scoped. Modules do not read or write another module's tables directly.
9. **Files in a slice are co-located.** A feature slice contains its `Request`, `Response`, `Command`, `Handler`, `Validator`, and `Endpoint` in the same folder. Do not split them across layers.

Violating these will cause architectural tests to fail. The failure messages are written to tell you *what* rule was violated and *how* to fix it. Read them literally.

---

## Adding a feature — the standard workflow

> For the full step-by-step, see `docs/how-to/add-a-slice.md`.

High-level:

1. Identify the module the feature belongs to.
2. Create a folder under that module's `Features/` directory, named for the feature.
3. Create the six slice files: `{Name}.Request.cs`, `{Name}.Response.cs`, `{Name}.Command.cs` (or `Query`), `{Name}.Handler.cs`, `{Name}.Validator.cs`, `{Name}.Endpoint.cs`.
4. The endpoint maps the `Request` to the `Command`, dispatches via `IMessageBus`, and maps the `Result<T>` to an HTTP response (success → DTO, failure → `ProblemDetails`).
5. Register the endpoint in the module's `MapEndpoints` extension.
6. Add integration tests under the module's `IntegrationTests` project.

If a slice needs cross-module data, publish a domain event from the owning module and subscribe to it in the consuming module's `Integration/` folder. Never reach across.

Use the `dotnet new` templates:

```bash
dotnet new modulith-slice --module Orders --name CancelOrder
```

---

## Common commands

```bash
# Build everything
dotnet build

# Run the full stack via Aspire (Postgres, Redis, Mailpit, API)
dotnet run --project src/AppHost

# Run all tests
dotnet test

# Run only fast tests (unit + architectural)
dotnet test --filter "Category!=Integration&Category!=Smoke"

# Run one module's tests
dotnet test tests/Modules/Orders/Modulith.Modules.Orders.IntegrationTests

# Add a migration for a specific module
dotnet ef migrations add <Name> \
  --project src/Modules/Orders/Modulith.Modules.Orders \
  --context OrdersDbContext \
  --output-dir Persistence/Migrations

# Scaffold a new slice
dotnet new modulith-slice --module <Module> --name <FeatureName>

# Scaffold a new module
dotnet new modulith-module --name <ModuleName>
```

---

## Footguns

Mistakes you will make if you aren't warned.

- **Don't inject `IConfiguration`.** Define an `Options` class, bind it in `*Module.cs` registration, inject `IOptions<T>`. The arch tests will catch it; save yourself the cycle.
- **Don't reach across module boundaries.** If module A needs something from module B, module B exposes it as a contract (command, query, or event). No shared EF context, no shared database access, no direct service calls.
- **Don't edit `Directory.Packages.props` casually.** Package choices are deliberate and documented in ADRs. Adding a dependency requires an ADR or explicit instruction.
- **Don't edit migrations after they're committed.** Create a new migration. Editing committed migrations breaks everyone else's database.
- **Don't cache across module boundaries.** Each module uses its own cache key prefix (`{module}:*`). Never invalidate another module's cache keys.
- **Don't throw for expected failures.** `Result.Fail(...)` for validation, missing entities, business rule violations. Exceptions are bugs or infrastructure.
- **Don't put business logic in handlers.** Handlers orchestrate; aggregates enforce invariants. If a handler has more than a few lines of branching, the logic belongs on the aggregate.
- **Don't forget the outbox.** If a handler publishes integration events, the outbox ensures they're sent even on failure. Wolverine's `AutoApplyTransactions` handles this, but only if the handler is discovered correctly. Verify with an integration test.
- **Don't make Wolverine handler types `internal`.** Wolverine requires handlers to be `public`, `concrete`, and closed. An `internal` handler compiles but throws `ArgumentOutOfRangeException: Handler types must be public` at startup. If a constructor parameter type is `internal`, make the type `public` — not the handler `internal`. Being `public` inside an internal project doesn't violate boundary rules; other modules still can't reference it.
- **Don't skip the `.Contracts` project for a new module.** Even if nothing is exposed yet, the project must exist. Other modules will eventually subscribe.
- **Don't bypass `IBlobStore`.** No direct `File.WriteAllBytes` for user content. The abstraction exists for a reason and the two-phase commit lifecycle depends on it.
- **Don't add ASP.NET Identity.** We use a lightweight custom `User` aggregate by deliberate choice (ADR-0007).

---

## When to ask vs. when to proceed

**Ask first if:**
- The change requires a new top-level dependency (new NuGet package referenced by multiple modules).
- The change touches `Directory.Packages.props` or `Directory.Build.props`.
- The change alters module boundaries (new module, removing a module, moving features between modules).
- The change affects the domain model in a way that requires a migration *and* changes contracts exposed to other modules.
- The change introduces a new cross-cutting concern (new shared infrastructure).
- The request is ambiguous about module ownership of a feature.
- The change would violate an invariant above.

**Proceed autonomously if:**
- Adding a new feature slice inside an existing module following the standard workflow.
- Adding a new field to a DTO (with migration if it's persisted).
- Writing tests for existing functionality.
- Fixing an obvious bug in a well-scoped area.
- Refactoring within a single slice.
- Improving error messages or validation.

When in doubt, ask. The cost of asking once is less than the cost of unwinding a wrong assumption.

---

## How to respond to architectural test failures

Architectural tests are designed to produce specific, actionable failures. When one fails:

1. Read the failure message. It names the rule and the offending type.
2. Check `docs/adr/` for the ADR that justifies the rule. Understand *why* before changing anything.
3. In 99% of cases, the correct action is to change your code to fit the rule.
4. If you believe the rule itself is wrong, stop and ask. Changing an architectural test requires an ADR update.

---

## How to respond to Result failures in handlers

When a handler returns `Result.Fail(error)`, the endpoint converts it to a `ProblemDetails` response. You do not throw; you return. The conversion mapping is defined once, in the shared kernel. If you need a new error category, extend the shared error types — don't invent ad-hoc ones.

---

## Things you should not touch autonomously

- `Directory.Packages.props` — package version management
- `Directory.Build.props` — shared MSBuild properties
- `.editorconfig` / `.globalconfig` — analyzer and style configuration
- Top-level `Program.cs` composition — module registration order and host configuration
- Wolverine root configuration (bus setup, transports, durability)
- Aspire AppHost resource declarations
- `docs/adr/` — ADRs are appended (new files), not rewritten, except with explicit instruction
- Existing migrations
- The CI pipeline configuration

If a change requires touching these, ask.

---

## Key ADRs to read first

If you read five ADRs, read these:

- ADR-0001: Modular Monolith Architecture
- ADR-0002: Vertical Slice Architecture
- ADR-0003: Wolverine for Messaging, Outbox, and Background Jobs
- ADR-0004: Result Pattern Over Exceptions
- ADR-0015: Architectural Tests for Boundary Enforcement

---

## Style expectations

- File-scoped namespaces everywhere.
- `sealed` by default on classes unless inheritance is intended.
- `required` init-only properties for DTOs and value objects where applicable.
- `record` for messages (commands, queries, events, requests, responses). `class` for entities.
- Nullable reference types are enabled. Treat warnings as errors.
- No underscore prefixes on private fields; use `this.` if disambiguation is needed.
- No `#region` blocks.
- Async methods end with `Async` suffix.
- One public type per file. The file name matches the type.

---

## Documentation expectations

When you add a feature, you do not need to write documentation unless:

- The feature introduces a new pattern (requires updating the relevant how-to)
- The feature changes a public contract (update the affected module's README or OpenAPI annotations)
- The feature crosses module boundaries in a new way (update `docs/architecture.md` if the diagram changes)

When in doubt, leave documentation to a reviewer.
