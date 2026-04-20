# How-to: Add a New Module

A module is a vertical slice of business capability — its own domain, persistence, endpoints, and public contract. Adding one touches several places. This guide walks through it.

For the architectural reasoning, see [`adr/0001-modular-monolith.md`](../adr/0001-modular-monolith.md) and [`adr/0005-module-communication-patterns.md`](../adr/0005-module-communication-patterns.md).

---

## When to add a module

Add a module when:

- A new business capability has its own domain vocabulary and invariants.
- The capability would have its own database schema.
- Other modules might need to subscribe to its events.
- Extracting it to a separate service someday is plausible.

Do NOT add a module for:

- A single feature. Features are slices inside existing modules.
- A cross-cutting concern. Those live in `Shared.Infrastructure`.
- Shared primitives. Those go in `Shared.Kernel`.

If you're unsure, ask.

---

## The scaffold (preferred)

```bash
dotnet new modulith-module --name Inventory
```

This produces:

- `src/Modules/Inventory/Modulith.Modules.Inventory/` (internal project)
- `src/Modules/Inventory/Modulith.Modules.Inventory.Contracts/` (public project)
- `tests/Modules/Inventory/Modulith.Modules.Inventory.UnitTests/`
- `tests/Modules/Inventory/Modulith.Modules.Inventory.IntegrationTests/`

Plus:

- Correct csproj references
- Stub `InventoryModule.cs` with registration extensions
- Stub `InventoryDbContext.cs` with the `inventory` schema
- Stub `InventoryOptions.cs` with validation wiring
- Stub `CLAUDE.md` for the module
- Empty `Features/`, `Domain/`, `Integration/`, `Seeding/`, `Persistence/Configurations/`, `Persistence/Migrations/`
- Empty `Events/` in the Contracts project

---

## Doing it manually

If you must (e.g., the scaffold doesn't exist yet or you're customizing), here's the full set:

### 1. Create the project directories

```
src/Modules/<Module>/
├── Modulith.Modules.<Module>/
│   ├── Modulith.Modules.<Module>.csproj
│   ├── <Module>Module.cs
│   ├── <Module>Options.cs
│   ├── Domain/
│   ├── Features/
│   ├── Integration/
│   ├── Persistence/
│   │   ├── <Module>DbContext.cs
│   │   ├── Configurations/
│   │   └── Migrations/
│   ├── Seeding/
│   └── CLAUDE.md
└── Modulith.Modules.<Module>.Contracts/
    ├── Modulith.Modules.<Module>.Contracts.csproj
    ├── Commands/
    ├── Queries/
    ├── Events/
    └── Dtos/
```

### 2. Create both csproj files

**Internal project references:**

- `Modulith.Shared.Kernel`
- `Modulith.Shared.Infrastructure`
- `Modulith.Modules.<Module>.Contracts` (its own contracts)
- Other modules' `.Contracts` projects only if subscribing to their events

**Contracts project references:**

- `Modulith.Shared.Kernel`
- `Modulith.Shared.Contracts`
- Nothing else.

### 3. Write `<Module>Module.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Modulith.Modules.Inventory;

public static class InventoryModule
{
    public static IServiceCollection AddInventoryModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<InventoryOptions>()
            .Bind(configuration.GetSection("Modules:Inventory"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddDbContext<InventoryDbContext>((sp, opts) =>
        {
            opts.UseNpgsql(configuration.GetConnectionString("db"));
            // Audit interceptor + other shared setup is applied in ModuleDbContext base
        });

        // Wolverine discovers handlers via assembly scanning configured in Program.cs
        // Add module-specific services here (non-handler, non-controller)

        return services;
    }

    public static IEndpointRouteBuilder MapInventoryEndpoints(this IEndpointRouteBuilder app)
    {
        // Register endpoints from each slice
        return app;
    }
}
```

### 4. Write `<Module>Options.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace Modulith.Modules.Inventory;

public sealed class InventoryOptions
{
    [Required]
    public required string BlobContainer { get; init; }

    [Range(1, 10000)]
    public int MaxItemsPerSku { get; init; } = 1000;
}
```

### 5. Write `<Module>DbContext.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Modulith.Shared.Infrastructure.Persistence;

namespace Modulith.Modules.Inventory.Persistence;

public sealed class InventoryDbContext : ModuleDbContext
{
    public InventoryDbContext(DbContextOptions<InventoryDbContext> options) : base(options) { }

    protected override string Schema => "inventory";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);  // applies conventions + shared configs
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(InventoryDbContext).Assembly);
    }
}
```

### 6. Register in `Api/Program.cs`

```csharp
// In the module registration block
builder.Services
    .AddUsersModule(builder.Configuration)
    .AddOrdersModule(builder.Configuration)
    .AddInventoryModule(builder.Configuration);   // ← add

// In the endpoint mapping block
app
    .MapUsersEndpoints()
    .MapOrdersEndpoints()
    .MapInventoryEndpoints();   // ← add
```

### 7. Register Wolverine assembly scanning

In `Program.cs`:

```csharp
builder.Host.UseWolverine(opts =>
{
    opts.Discovery.IncludeAssembly(typeof(InventoryModule).Assembly);
    // ... other modules
});
```

### 8. Add the first migration (once you have entities)

```bash
dotnet ef migrations add Initial \
  --project src/Modules/Inventory/Modulith.Modules.Inventory \
  --context InventoryDbContext \
  --output-dir Persistence/Migrations
```

### 9. Add module's `CLAUDE.md`

A short file describing:

- What this module does
- Domain vocabulary (ubiquitous language)
- Non-obvious invariants
- Cross-module dependencies (which other modules it subscribes to)
- Known footguns

Three to five paragraphs. Append detail as the module grows.

### 10. Add test projects

Two projects under `tests/Modules/<Module>/`:

- `Modulith.Modules.<Module>.UnitTests` — references the internal project + Shouldly
- `Modulith.Modules.<Module>.IntegrationTests` — references the internal project + TestSupport + Testcontainers + Shouldly + Verify

Add `IntegrationTests` xUnit collection fixture:

```csharp
[CollectionDefinition("InventoryModule")]
public sealed class InventoryModuleCollection : ICollectionFixture<InventoryApiFixture> { }

public sealed class InventoryApiFixture : ApiTestFixture
{
    // inherits Testcontainers + WebApplicationFactory<Program> lifecycle
}
```

---

## Verification

After setup, confirm:

1. `dotnet build` succeeds.
2. `dotnet test tests/Modulith.Architecture.Tests` passes (no boundary violations).
3. `dotnet run --project src/AppHost` boots without errors.
4. The module appears registered in logs at startup.
5. An empty OpenAPI section exists for the module (even with no endpoints yet).

---

## Common mistakes

- **Forgot to create the Contracts project.** The arch test will fail because the pair is required.
- **Referenced another module's internal project.** The arch test will tell you — replace with the Contracts reference.
- **Forgot to register in Program.cs.** Build passes, module never runs.
- **Same schema as another module.** Migrations will conflict. Each schema must be unique.
- **Missed Wolverine assembly registration.** Handlers won't be discovered.
- **Didn't add CLAUDE.md.** Lowers future agent effectiveness in this module.

---

## After the module exists

The typical next steps:

1. Model the aggregate in `Domain/` — start with one, add more as needed.
2. Add an initial slice under `Features/` (e.g., a create or register endpoint).
3. Write integration tests for the slice.
4. Add a seeder if the module has meaningful dev data.
5. Expose contract events as other modules need them.

See [`add-a-slice.md`](add-a-slice.md).

---

## Related

- [`cross-module-events.md`](cross-module-events.md)
- [`work-with-migrations.md`](work-with-migrations.md)
- [`adr/0001-modular-monolith.md`](../adr/0001-modular-monolith.md)
- [`adr/0005-module-communication-patterns.md`](../adr/0005-module-communication-patterns.md)
- [`adr/0023-per-module-dbcontext.md`](../adr/0023-per-module-dbcontext.md)
