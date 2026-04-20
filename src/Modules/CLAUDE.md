# CLAUDE.md — Modules

This directory holds all business modules. Each module is a vertical slice of business capability with its own domain, persistence, endpoints, and public contract.

For the repo-wide operating manual, see [`/CLAUDE.md`](../../CLAUDE.md).

---

## A module's shape

Every module is two projects:

```
<Module>/
├── Modulith.Modules.<Module>/                # Internal project
│   ├── Domain/                               # Aggregates, value objects, internal events
│   ├── Features/                             # Vertical slices
│   │   └── <FeatureName>/
│   │       ├── <Feature>.Request.cs
│   │       ├── <Feature>.Response.cs
│   │       ├── <Feature>.Command.cs  (or .Query.cs)
│   │       ├── <Feature>.Handler.cs
│   │       ├── <Feature>.Validator.cs
│   │       └── <Feature>.Endpoint.cs
│   ├── Integration/                          # Handlers for OTHER modules' public events
│   ├── Persistence/
│   │   ├── <Module>DbContext.cs
│   │   ├── Configurations/
│   │   └── Migrations/
│   ├── Seeding/                              # IModuleSeeder implementations
│   └── <Module>Module.cs                     # Registration extensions
└── Modulith.Modules.<Module>.Contracts/      # Public project
    ├── Commands/                             # If other modules can command this one
    ├── Queries/                              # If other modules can query this one
    ├── Events/                               # Integration events published by this module
    └── Dtos/                                 # Shared types used in the above
```

See [`docs/architecture.md`](../../docs/architecture.md) for full detail.

---

## Non-negotiables

1. **Two projects per module.** Internal and `.Contracts`. Both exist even if `.Contracts` is empty at first — it will be populated.
2. **The internal project references the `.Contracts` project, not vice versa.**
3. **Other modules reference only the `.Contracts` project.** Never the internal project.
4. **The DbContext is module-scoped.** Own schema, own migration history, own migrations folder.
5. **Integration events are `record`s with a version suffix.** `OrderPlacedV1`, not `OrderPlaced`.
6. **Internal domain events are separate from integration events.** Even when they carry the same data — ADR-0006 explains why.

---

## Adding a new module

Prefer the scaffold:

```bash
dotnet new modulith-module --name Inventory
```

This produces:

- `src/Modules/Inventory/Modulith.Modules.Inventory/` with the standard folder structure
- `src/Modules/Inventory/Modulith.Modules.Inventory.Contracts/` with an empty `Events/` folder
- `tests/Modules/Inventory/Modulith.Modules.Inventory.UnitTests/`
- `tests/Modules/Inventory/Modulith.Modules.Inventory.IntegrationTests/`
- Correct project references, csproj metadata, and namespace conventions
- Stub `InventoryModule.cs` with `AddInventoryModule` and `MapInventoryEndpoints` extensions
- Stub `InventoryDbContext.cs` pointing at the `inventory` schema
- Stub `CLAUDE.md` for the module

After scaffolding:

1. Register the module in `Api/Program.cs` (in the `AddXxxModule` block).
2. Register the endpoints in `Api/Program.cs` (in the `MapXxxEndpoints` block).
3. Add `InventoryOptions` and bind from `Modules:Inventory`.

If you add the module manually (without the template), confirm all of the above are present before committing. The architectural tests will catch missing pieces, but not all of them.

---

## Adding a feature slice to an existing module

Prefer the scaffold:

```bash
dotnet new modulith-slice --module Orders --name CancelOrder
```

This produces the six files (`Request`, `Response`, `Command`, `Handler`, `Validator`, `Endpoint`) with correct namespaces and stub content, plus an integration test stub.

After scaffolding:

1. Fill in the `Request` and `Response` properties.
2. Fill in the `Command` (usually mirrors the Request with typed IDs).
3. Implement the `Handler` — load aggregates, invoke methods, return `Result<T>`.
4. Add `Validator` rules.
5. Wire the `Endpoint` registration in the module's `MapXxxEndpoints` extension.
6. Flesh out the integration test.

For full details, see [`docs/how-to/add-a-slice.md`](../../docs/how-to/add-a-slice.md).

---

## Cross-module communication

The three permitted patterns (see ADR-0005):

**Integration events (most common):**
- Module A publishes `OrderPlacedV1` from `Orders.Contracts.Events`.
- Module B references `Orders.Contracts`, writes a handler in `Integration/`.
- Wolverine's outbox delivers the event.

> **Handler visibility:** All Wolverine handler classes — feature handlers and integration subscribers alike — must be `public`. An `internal` handler compiles but causes `ArgumentOutOfRangeException: Handler types must be public` at startup. If a dependency type is `internal`, make that type `public` rather than making the handler `internal`.

**Queries:**
- Module A exposes `GetUserByIdQuery` in `Users.Contracts.Queries`.
- Module B sends via `IMessageBus.InvokeAsync<UserDto>(query)`.

**Commands (rare):**
- Module A exposes `DeactivateUserCommand` in `Users.Contracts.Commands`.
- Module B sends via `IMessageBus.InvokeAsync<Result>(command)`.
- Usually a sign the boundary is wrong — prefer events.

**Never:**
- Reference another module's internal project.
- Inject another module's internal services.
- Join across schemas.
- Share a DbContext.

---

## Domain guidance

Domain code (under `Domain/`) must not reference infrastructure. No EF Core, no ASP.NET, no Wolverine, no FluentValidation, no HybridCache, no FeatureManagement. Enforced by architectural tests.

Aggregates:
- Inherit `AggregateRoot<TId>` from `Shared.Kernel`.
- Have a private constructor.
- Expose a public static `Create(...)` returning `Result<TSelf>`.
- Have no public setters.
- Raise domain events via `RaiseEvent(...)`.
- Expose state changes as methods returning `Result`.

Value objects:
- `sealed record` with validation in a static factory method.
- Equality is by value (free with records).

Strongly-typed IDs:
- Derive from `TypedId<T>` or similar base in `Shared.Kernel`.
- `UserId`, not `Guid`, in method signatures and entity properties.

See ADR-0009 for the full reasoning.

---

## Persistence guidance

- One `DbContext` per module, in `Persistence/`.
- `modelBuilder.HasDefaultSchema("<module>")` in `OnModelCreating`.
- Entity configurations as separate classes in `Persistence/Configurations/` (one per aggregate).
- Never declare foreign keys that cross schemas. Cross-module references are `Guid` columns with no FK.
- Migrations go in `Persistence/Migrations/` and are scoped to this module's context.
- Apply the `AuditableEntitySaveChangesInterceptor` (from `Shared.Infrastructure`) in `OnConfiguring`.

See ADR-0023.

---

## Module registration

Each module has a `<Module>Module.cs` with two extension methods:

```csharp
public static IServiceCollection Add<Module>Module(
    this IServiceCollection services,
    IConfiguration configuration)
{
    // Bind options
    services.AddOptions<<Module>Options>()
        .Bind(configuration.GetSection("Modules:<Module>"))
        .ValidateDataAnnotations()
        .ValidateOnStart();

    // Register DbContext
    services.AddDbContext<<Module>DbContext>(...);

    // Register module-internal services
    // (Wolverine discovers handlers via assembly scanning)

    return services;
}

public static IEndpointRouteBuilder Map<Module>Endpoints(
    this IEndpointRouteBuilder app)
{
    // Register endpoints from each slice
    PlaceOrderEndpoint.Map(app);
    CancelOrderEndpoint.Map(app);
    // ...
    return app;
}
```

`Api/Program.cs` calls both in its composition phase.

---

## Things you should not touch

- The base `ModuleDbContext` in `Shared.Infrastructure` — it applies global conventions. Changes affect every module.
- `ICurrentUser` and its implementation — used by auditing and authorization.
- The shared `DomainEvent`, `AggregateRoot`, `Result` primitives in `Shared.Kernel` — changes ripple everywhere.

If you need to change these, ask.
