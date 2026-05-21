# AGENTS.md - Modules

This directory holds all business modules. Each module is a vertical slice of business capability with its own domain, persistence, endpoints, and public contract.

For the repo-wide operating manual, see [`/AGENTS.md`](../../AGENTS.md).

---

## A module's shape

Every module is two projects:

```
<Module>/
+-- Modulith.Modules.<Module>/                # Internal project
|   +-- Domain/                               # Aggregates, value objects, internal events
|   +-- Features/                             # Vertical slices
|   |   +-- <FeatureName>/
|   |       +-- <Feature>.Request.cs
|   |       +-- <Feature>.Response.cs
|   |       +-- <Feature>.Command.cs  (or .Query.cs)
|   |       +-- <Feature>.Handler.cs
|   |       +-- <Feature>.Validator.cs
|   |       +-- <Feature>.Endpoint.cs
|   +-- Integration/                          # Handlers for OTHER modules' public events
|   +-- Persistence/
|   |   +-- <Module>DbContext.cs
|   |   +-- Configurations/
|   |   +-- Migrations/
|   +-- Seeding/                              # IModuleSeeder implementations
|   +-- <Module>Module.cs                     # Registration extensions
+-- Modulith.Modules.<Module>.Contracts/      # Public project
    +-- Commands/                             # If other modules can command this one
    +-- Queries/                              # If other modules can query this one
    +-- Events/                               # Integration events published by this module
    +-- Dtos/                                 # Shared types used in the above
```

See [`docs/architecture.md`](../../docs/architecture.md) for full detail.

## Organization-scoped modules

Organization support is opt-in. A module that owns organization-scoped resources stores an `OrganizationId` value in its own schema and exposes routes under:

```text
/v1/organizations/{organizationRef}/...
```

Resolve `organizationRef` (ID or slug) at the endpoint boundary. Commands, queries, persisted rows, and integration events use the durable organization ID.

Use the shared scoped-authorization abstractions with `OrganizationScope` from `Organizations.Contracts`. Do not reference the Organizations internal project, query its tables, or add cross-schema foreign keys.

Platform override for global admins is explicit per endpoint/policy call. Never model global admins as hidden organization members.

---

## Non-negotiables

1. **Two projects per module.** Internal and `.Contracts`. Both exist even if `.Contracts` is empty at first - it will be populated.
2. **The internal project references the `.Contracts` project, not vice versa.**
3. **Other modules reference only the `.Contracts` project.** Never the internal project.
4. **The DbContext is module-scoped.** Own schema, own migration history, own migrations folder.
5. **Integration events are `record`s with a version suffix.** `OrderPlacedV1`, not `OrderPlaced`.
6. **Internal domain events are separate from integration events.** Even when they carry the same data - ADR-0006 explains why.

---

## Adding a new module

Prefer the scaffold:

```bash
dotnet new modulith-module --name Inventory
```

This produces:

- `src/Modules/Inventory/Modulith.Modules.Inventory/` (internal project)
- `src/Modules/Inventory/Modulith.Modules.Inventory.Contracts/` with an `Events/` folder
- Correct project references, csproj metadata, and namespace conventions
- `InventoryModule.cs` with permissions, DbContext, health checks, telemetry, GDPR stubs, dev seeding, `AddInventoryHandlers` (Wolverine), and `MapInventoryEndpoints` extensions
- `InventoryModuleInstaller.cs` implementing `IModuleInstaller` for API autodiscovery
- `InventoryDbContext.cs` with the `inventory` schema
- `InventoryRoutes.cs` with route prefix constants
- `InventoryErrors.cs` stub for ErrorOr error definitions
- `InventoryPermissions.cs`, `InventoryTelemetry.cs`, no-op GDPR exporter/eraser, and `InventoryDevSeeder.cs`

Test projects, `AGENTS.md`, `InventoryOptions.cs`, and domain/feature subfolders must be added manually after scaffolding.

After scaffolding:

1. Confirm `InventoryModuleInstaller` was created. The API auto-discovers module installers from referenced `Modulith.Modules.*` assemblies.
2. Add `InventoryOptions` and bind from `Modules:Inventory` if the module has configuration.

If you add the module manually (without the template), confirm all of the above are present before committing. The architectural tests will catch missing pieces, but not all of them.

---

## Adding a feature slice to an existing module

Prefer the scaffold:

```bash
dotnet new modulith-slice --module Orders --name CancelOrder
```

This produces the six files (`Request`, `Response`, `Command`, `Handler`, `Validator`, `Endpoint`) with correct namespaces, permission-protected endpoint metadata, and stub content. `dotnet new modulith-command-slice` is the explicit equivalent for command/write slices.

For read/query slices:

```bash
dotnet new modulith-query-slice --module Orders --name GetOrder
```

This produces `Response`, `Query`, `Handler`, and an authenticated `Endpoint` protected by the module's read permission. The integration test must be written manually.

For an integration event/subscriber pair owned by a module:

```bash
dotnet new modulith-integration-pair --module Orders --name OrderPlaced
```

This produces `Orders.Contracts.Events.OrderPlacedV1` and `Integration/Subscribers/OnOrderPlacedHandler`. Register the handler in `AddOrdersHandlers`; if the subscriber consumes another module's event, move the subscriber to the consuming module and reference only the publisher's `.Contracts` project.

After scaffolding:

1. Fill in the `Request` and `Response` properties.
2. Fill in the `Command` (usually mirrors the Request with typed IDs).
3. Implement the `Handler` - load aggregates, invoke methods, return `ErrorOr<T>` or `ErrorOr<Success>`.
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

> **Handler visibility:** All Wolverine handler classes - feature handlers and integration subscribers alike - must be `public`. An `internal` handler compiles but causes `ArgumentOutOfRangeException: Handler types must be public` at startup. If a dependency type is `internal`, make that type `public` rather than making the handler `internal`.

**Queries:**
- Module A exposes `GetUserByIdQuery` in `Users.Contracts.Queries`.
- Module B sends via `IMessageBus.InvokeAsync<UserDto>(query)`.

**Commands (rare):**
- Module A exposes `DeactivateUserCommand` in `Users.Contracts.Commands`.
- Module B sends via `IMessageBus.InvokeAsync<ErrorOr<Success>>(command)` for no-payload commands, or `ErrorOr<TResponse>` when a response DTO is returned.
- Usually a sign the boundary is wrong - prefer events.

**Never:**
- Reference another module's internal project.
- Inject another module's internal services.
- Join across schemas.
- Share a DbContext.

---

## Domain guidance

Domain code (under `Domain/`) must not reference infrastructure. No EF Core, no ASP.NET, no Wolverine, no FluentValidation, no HybridCache, no FeatureManagement. Enforced by architectural tests.

For aggregate, value object, and typed ID conventions, read any existing aggregate (e.g. `User`, `Product`) or see ADR-0009.

---

## Persistence guidance

- Never declare foreign keys that cross schemas. Cross-module references are `Guid` columns with no FK.
- For DbContext setup, schema naming, and configuration conventions, read any existing module's `Persistence/` folder or see ADR-0023.

---

## Module registration

Each module has a `<Module>Module.cs` with three extension methods: `Add<Module>Module` (services + options + DbContext + validators), `Add<Module>Handlers` (Wolverine handler discovery), and `Map<Module>Endpoints` (endpoint wiring). Each module also has a `<Module>ModuleInstaller` implementing `IModuleInstaller`; the API auto-discovers installers and calls those three methods through the installer. See any existing `*Module.cs` and `*ModuleInstaller.cs` for the pattern.

---

## Things you should not touch

- The base `ModuleDbContext` in `Shared.Infrastructure` - it applies global conventions. Changes affect every module.
- `ICurrentUser` and its implementation - used by auditing and authorization.
- The shared `DomainEvent`, `AggregateRoot`, and base kernel primitives in `Shared.Kernel` - changes ripple everywhere. Expected failures use the `ErrorOr` package.

If you need to change these, ask.
