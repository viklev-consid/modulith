---
name: ef-migration
description: Workflow for adding and reviewing EF Core migrations in Modulith. Covers per-module schemas, migration commands, destructive-change patterns, and what not to edit.
---

# EF Migration

Use this skill when a persisted model change requires a database schema change.

Typical triggers:

- adding or removing an entity property
- changing entity configuration or table shape
- adding an index, constraint, or table
- adding a persistence feature such as a processed-messages table

Do not use this skill when:

- the change is only domain logic with no persistence impact
- the change is only query behavior with no schema change
- the task is to edit a committed migration without explicit instruction

## Read first

Before changing persistence or migrations, read:

1. `/CLAUDE.md`
2. `/src/Modules/CLAUDE.md`
3. `docs/how-to/work-with-migrations.md`
4. `docs/adr/0023-per-module-dbcontext.md`
5. one nearby migration in the same module

## Per-module conventions

Every module owns its own persistence boundary.

Required conventions:

- one DbContext per module
- one schema per module
- migrations live in `Persistence/Migrations/`
- migration history is per module

Never design a migration that couples two modules' schemas together.

## Decide whether a migration is needed

You need a migration when a change affects persisted shape, including:

- columns
- tables
- indexes
- foreign keys inside the same module
- uniqueness constraints
- owned-type mappings

You usually do not need a migration for:

- pure handler logic
- DTO changes only
- validation changes only
- in-memory or computed-only behavior

## Canonical command

Generate migrations with the module project and explicit DbContext.

```bash
dotnet ef migrations add <Name> \
  --project src/Modules/<Module>/Modulith.Modules.<Module> \
  --context <Module>DbContext \
  --output-dir Persistence/Migrations
```

Migration names should be descriptive and in PascalCase.

Examples:

- `AddProcessedMessages`
- `RenameCustomerToClient`
- `IntroduceOrderLines`

## Safe workflow

Use this order:

1. make the model or configuration change
2. generate the migration
3. inspect the generated migration code
4. inspect SQL for non-trivial changes
5. run relevant integration tests

For non-trivial changes, generate the SQL script and review it before you trust the migration.

## Do not edit committed migrations

This is a hard repo rule.

If the migration is already committed:

- do not delete it
- do not rewrite it
- add a new migration that moves the schema from the current state to the desired state

If the migration is not committed yet, you may remove and regenerate it as part of local iteration.

## Reverting before commit

Before a migration is committed, the safe local rollback workflow is:

1. update the database to the previous migration
2. remove the migration files
3. fix the model
4. regenerate

After commit, do not use removal as a fix strategy.

## Destructive-change pattern

Destructive changes must be phased.

Use a two-step or three-step approach:

1. add the new column or table in a backward-compatible way
2. backfill data
3. switch code to the new shape
4. only later remove the old column or table

Examples that need phasing:

- renaming columns that hold live data
- splitting one entity into several tables
- making a nullable column non-null on existing rows
- replacing one identity or reference scheme with another

Do not drop live data in one shot unless the task explicitly allows it and the impact is understood.

## Data migration rules

Small, mechanical backfills are fine in an EF migration.

Examples:

- populating a new default value
- copying data from one column to another
- setting a status based on an existing timestamp

Do not embed large business workflows or complex branching in migration code. Use a dedicated one-off process if the backfill is complex.

## Cross-module data rules

Never add:

- cross-schema foreign keys between modules
- navigation properties across modules
- queries that require one module to join another module's tables

Cross-module references should remain logical references such as `Guid` values, validated at the application layer.

## Review checklist

For every non-trivial migration, inspect:

- nullability changes
- default values on existing rows
- index creation or removal
- rename versus drop-and-add behavior
- raw SQL correctness
- data-loss risk

If EF generated a destructive diff when a rename was intended, fix the mapping or the migration before proceeding.

## Testing and validation

Integration tests run migrations against fresh Postgres containers, so migration breakage should fail quickly.

After generating or changing a migration, prefer these checks:

- the target module's integration tests
- the architecture test suite if project structure changed
- a focused build of the touched module

## Common mistakes

Avoid these:

- editing a committed migration
- forgetting `--context` in a multi-DbContext solution
- placing migrations outside `Persistence/Migrations/`
- adding a non-null column without a strategy for existing data
- dropping a populated column in the same change that introduces the replacement
- adding cross-module foreign keys

## Ask-first cases

Stop and ask before proceeding if:

- the change affects multiple modules' persisted contracts
- the change needs a top-level infrastructure decision
- the migration would require editing an existing committed migration
- the change also alters module boundaries or public contracts in a way that needs design input

## Definition of done

A migration change is complete when:

- the schema change belongs to one module boundary
- the migration lives in the correct module folder
- committed migrations were not rewritten
- destructive changes are phased safely
- non-trivial SQL has been inspected
- the relevant tests pass

## Reference material

Use these as the source of truth:

- `docs/how-to/work-with-migrations.md`
- `docs/adr/0023-per-module-dbcontext.md`
- `/CLAUDE.md`
- `/src/Modules/CLAUDE.md`