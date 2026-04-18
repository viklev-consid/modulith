# How-to: Work With Migrations

Each module has its own `DbContext`, its own schema, and its own migration history. This guide covers the common operations.

For the architectural reasoning, see [`../adr/0023-per-module-dbcontext.md`](../adr/0023-per-module-dbcontext.md).

---

## Prerequisites

```bash
dotnet tool install --global dotnet-ef
# or update if already installed:
dotnet tool update --global dotnet-ef
```

Every module has:

- Its own `DbContext` (e.g., `OrdersDbContext`).
- Its own schema (e.g., `orders`).
- Its own `Persistence/Migrations/` folder.
- Its own `__EFMigrationsHistory_<Module>` table in the database.

---

## Adding a migration

```bash
dotnet ef migrations add <n> \
  --project src/Modules/Orders/Modulith.Modules.Orders \
  --context OrdersDbContext \
  --output-dir Persistence/Migrations
```

- `<n>`: descriptive, in PascalCase. `AddOrderStatusColumn`, `IntroduceOrderLines`, `RenameCustomerToClient`.
- `--project`: the module's internal project.
- `--context`: the module's DbContext class.
- `--output-dir`: always `Persistence/Migrations` for consistency.

**The API host project is the startup project** for EF tooling (because that's where connection strings are configured). The tool picks it up automatically from the solution.

---

## Applying migrations

In development, migrations run automatically on host startup. In production, migration strategy is deployment-specific (see [`CONFIG.md`](../../CONFIG.md)); the template does not auto-migrate in production by default.

Manual migration (dev or staging):

```bash
dotnet ef database update \
  --project src/Modules/Orders/Modulith.Modules.Orders \
  --context OrdersDbContext
```

Apply to a specific migration:

```bash
dotnet ef database update <MigrationName> \
  --project src/Modules/Orders/Modulith.Modules.Orders \
  --context OrdersDbContext
```

---

## Reverting a migration (before commit)

```bash
# Roll back the database
dotnet ef database update <PreviousMigrationName> \
  --project src/Modules/Orders/Modulith.Modules.Orders \
  --context OrdersDbContext

# Remove the migration files
dotnet ef migrations remove \
  --project src/Modules/Orders/Modulith.Modules.Orders \
  --context OrdersDbContext
```

Only safe **before** the migration is committed. After commit, see the next section.

---

## Reverting after commit (rule: don't)

**Do not edit or delete committed migrations.** Other contributors have applied them. Deleting breaks their databases.

Correct pattern: create a **new** migration that reverses the change.

```bash
dotnet ef migrations add RevertAddOrderStatusColumn ...
```

Hand-edit the generated `Up`/`Down` methods if needed. The `Up` of the new migration does what the `Down` of the old one would have done.

---

## Inspecting the generated SQL

Without applying:

```bash
dotnet ef migrations script <FromMigration> <ToMigration> \
  --project src/Modules/Orders/Modulith.Modules.Orders \
  --context OrdersDbContext \
  --output migration.sql
```

Use this in PR review for non-trivial migrations — especially anything involving data transformation, indexes, or schema renames.

---

## Cross-schema constraints: don't

No foreign keys across modules. No navigation properties across modules. No `Include` across modules.

If `Orders.Orders` has a `CustomerId`, it's a `Guid` column — not an FK to `Users.Users.Id`.

Referential integrity is enforced at the application layer (by validating the reference exists when the order is placed). Periodic consistency checks (as a scheduled Wolverine job, if needed) can detect drift.

See [`adr/0023-per-module-dbcontext.md`](../adr/0023-per-module-dbcontext.md).

---

## Data migrations

EF migrations can include data changes in `Up`/`Down`:

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.AddColumn<string>(
        name: "Status",
        schema: "orders",
        table: "orders",
        type: "text",
        nullable: false,
        defaultValue: "Placed");

    migrationBuilder.Sql(@"
        UPDATE orders.orders
        SET ""Status"" = 'Shipped'
        WHERE ""ShippedAt"" IS NOT NULL;
    ");
}
```

For anything more complex than a UPDATE-SET, consider a separate code-driven data migration run via a one-shot Wolverine job or a dedicated console tool. Migrations aren't a good place for significant business logic.

---

## Renaming and refactoring

**Renaming a column.** Use `migrationBuilder.RenameColumn` — EF generates it if you use `[Column("NewName")]` or `.HasColumnName("NewName")` in the configuration.

**Renaming an entity class.** Rename the class, rename the entity configuration, generate a migration. If the entity's table name should also change, update the configuration.

**Splitting an entity.** Generate additions first (new tables/columns), deploy, backfill via code, then generate removals in a later migration. Two-phase to avoid downtime.

**Renaming a schema.** Rare and painful. Usually not worth it — the cost of cleanup exceeds the benefit. If unavoidable, raw SQL in a migration.

---

## Seeding vs. migrations

**Migrations** change schema. They may include small data fixes (e.g., backfilling a new non-null column), but should not insert business data.

**Seeders** ([`../adr/0026-module-seeders.md`](../adr/0026-module-seeders.md)) populate development or test data. They run separately from migrations, per environment, and are idempotent.

Don't insert "default" business data (roles, categories, feature defaults) from migrations unless they are genuinely structural (e.g., "Admin" role is required for the app to function). Prefer seeders.

---

## The migration history tables

Each module gets its own history table:

- `users.__EFMigrationsHistory`
- `orders.__EFMigrationsHistory`
- etc.

Configured in `ModuleDbContext.OnConfiguring` via `UseNpgsql(..., npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", schema))`.

This way modules migrate independently — applying Orders' migration doesn't touch Users' history.

---

## CI and migrations

Integration tests run migrations against the Testcontainers Postgres — every module's migrations apply to a fresh container on every CI run. A migration that fails to apply fails the build immediately.

For production deployment:

- **Option A**: apply migrations on host startup. Simple, but bad if the host starts in multiple replicas simultaneously (race conditions) or if migrations are slow (startup delay + health check failures).
- **Option B**: apply migrations as a separate step in the deployment pipeline, before the new host starts. Preferred for production.
- **Option C**: generate SQL scripts, apply out-of-band (DBA-managed).

The template defaults to auto-migrate in Development/Testing only. Production strategy is the team's decision.

---

## Common mistakes

- **Editing a committed migration.** Breaks other people's databases.
- **Skipping the `--context` flag.** With multiple DbContexts in the solution, EF tooling can't guess. Always specify.
- **Placing migrations outside `Persistence/Migrations/`.** Arch test catches this.
- **Cross-schema FKs.** Don't. Ever.
- **Adding a non-null column without a default.** Breaks the migration against existing data. Add as nullable, backfill, then make non-null in a later migration.
- **Dropping a column that has data.** Two-phase: add the new structure, migrate data, deploy; drop the old in a later migration.

---

## Related

- [`../adr/0023-per-module-dbcontext.md`](../adr/0023-per-module-dbcontext.md)
- [`../adr/0026-module-seeders.md`](../adr/0026-module-seeders.md)
- [`add-a-module.md`](add-a-module.md)
