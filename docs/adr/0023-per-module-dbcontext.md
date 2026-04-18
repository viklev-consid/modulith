# ADR-0023: DbContext and Schema Per Module

## Status

Accepted

## Context

In a modular monolith, database design mirrors architectural design. Options:

1. **One DbContext, one schema.** Every module's entities share one context. Easy, but couples migrations and lets modules reach into each other's tables through the context's `DbSet`s.
2. **One DbContext, schema per module.** Shared context but separate schemas. Slightly better isolation but modules still compile against each other's entity types.
3. **One DbContext per module, schema per module.** Each module has its own context and schema. Strong isolation, independent migrations, mirrors the module boundaries.
4. **Database per module.** Ultimate isolation, no cross-module queries possible even in principle. Operational overhead unjustified for a monolith.

Option 3 is the right fit for this template. It makes the architectural boundaries real at the data layer.

## Decision

Each module owns:

- Its own `DbContext` class (`OrdersDbContext`, `UsersDbContext`, ...).
- Its own schema (`orders`, `users`, ...) — configured via `modelBuilder.HasDefaultSchema("orders")`.
- Its own migrations, in its own `Persistence/Migrations/` folder.
- Its own `DbSet`s, referencing only its own entities.

All modules' contexts point at the **same physical database**. The separation is logical (schema + context), not physical. This keeps operations simple while preserving boundaries.

### Shared infrastructure

Cross-cutting EF concerns live in `Shared.Infrastructure`:

- `AuditableEntitySaveChangesInterceptor` (ADR-0011)
- A base `ModuleDbContext` class that applies common conventions (snake_case naming, UTC date handling, etc.)
- Shared type configurations (strongly-typed ID converters, value object converters)

Each module's context inherits `ModuleDbContext` and adds its entities.

### Migrations

Migrations are module-scoped. The `dotnet ef migrations add` command takes `--project` and `--context` to disambiguate:

```bash
dotnet ef migrations add AddOrderCancelledStatus \
  --project src/Modules/Orders/Modulith.Modules.Orders \
  --context OrdersDbContext \
  --output-dir Persistence/Migrations
```

Each context has its own migration history table (`__EFMigrationsHistory_Orders`, `__EFMigrationsHistory_Users`) so modules migrate independently.

On host startup, each module's migrations run in sequence. Order is determined by module registration order in `Api/Program.cs`. Modules with no cross-module data dependencies at the schema level can run in any order; there are no cross-module FKs (see below).

### No cross-schema foreign keys

Modules do **not** declare foreign keys across schemas. If the Orders module records a `UserId`, it's a simple `Guid` column with no FK constraint pointing at `users.users`. This is intentional:

- Preserves the principle that modules don't share tables.
- Enables independent migrations.
- Makes extraction to a separate service trivial later (the FK never existed to be broken).

Referential integrity between modules is enforced at the application layer — typically by the module that holds the reference validating existence via a query contract at the time the reference is created.

### What NOT to do

- Don't share a DbContext across modules.
- Don't declare navigation properties between aggregates in different modules.
- Don't write raw SQL that reads another module's tables.
- Don't declare cross-schema foreign keys.
- Don't share migration histories.

All of these are enforced or monitored by architectural tests.

## Consequences

**Positive:**

- Module boundaries exist at the data layer. Not possible to "accidentally" join across modules.
- Independent migrations — each module evolves its schema without coordination.
- Extraction to a service is a data migration (schema moves to a new DB), not a re-architecture.
- Clear ownership: "who can change the `orders.orders` table?" has one answer — the Orders module.

**Negative:**

- No cross-module FKs means no referential integrity at the DB level for cross-module references. A `UserId` in `orders.orders` can theoretically reference a non-existent user. Application-level validation and, optionally, periodic consistency checks compensate.
- Multiple migrations to run on deploy. Small cost.
- Reports that join across modules require a read model or a dedicated reporting query, not an ad-hoc cross-schema JOIN. For small reports, this is mildly annoying; for large ones, you want a read model anyway.
- Cannot use EF's `Include` across modules. Same mitigation — query the other module via contracts.

## Related

- ADR-0001 (Modular Monolith): the architectural principle.
- ADR-0005 (Module Communication): how modules get each other's data without sharing tables.
- ADR-0011 (Auditing): each module's context applies the audit interceptor.
- ADR-0015 (Architectural Tests): enforces the no-shared-DbContext rule.
