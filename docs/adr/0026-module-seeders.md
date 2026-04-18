# ADR-0026: IModuleSeeder Contract for Deterministic Seed Data

## Status

Accepted

## Context

A template is judged by its first-run experience. A developer clones the repo, runs `dotnet run`, and either sees something coherent or doesn't. If they have to manually create users, insert sample orders, or configure initial state, the template has already failed its first impression.

Seed data strategies:

1. **Nothing ships, users figure it out.** Common and bad.
2. **SQL scripts in a `seed/` folder.** Works, but brittle — schema changes break scripts.
3. **Hard-coded inserts in `Program.cs`.** Fastest to start, worst to maintain.
4. **Per-module seeder contracts.** Each module contributes its own seed data; the host discovers and invokes them.

Option 4 respects module boundaries (each module knows its own domain, so it seeds its own data) and scales as modules multiply.

Seeding also interacts with testing. Integration tests sometimes want "a user exists" as a precondition; the same seeding logic can be reused (at the module seeder level) rather than duplicated in test fixtures.

## Decision

### The contract

In `Shared.Kernel`:

```csharp
public interface IModuleSeeder
{
    SeedEnvironment Environment { get; }  // Development, Testing, All
    int Order { get; }                     // deterministic sequencing
    Task SeedAsync(IServiceProvider services, CancellationToken ct);
}

public enum SeedEnvironment { All, Development, Testing }
```

Each module that has seed data implements one or more seeders in its `Seeding/` folder:

```csharp
internal sealed class UsersDevSeeder : IModuleSeeder
{
    public SeedEnvironment Environment => SeedEnvironment.Development;
    public int Order => 10;

    public async Task SeedAsync(IServiceProvider services, CancellationToken ct)
    {
        var db = services.GetRequiredService<UsersDbContext>();
        if (await db.Users.AnyAsync(ct)) return;  // idempotent

        db.Users.Add(User.Create(Email.From("alice@example.com"), ...).Value);
        db.Users.Add(User.Create(Email.From("bob@example.com"), ...).Value);
        await db.SaveChangesAsync(ct);
    }
}
```

### Execution

On host startup in the `Development` environment, registered seeders with `SeedEnvironment.Development` or `All` run in `Order` sequence. In `Production`, only `All` seeders run (typically none).

For integration tests, test fixtures invoke seeders tagged `Testing` or `All` to establish known state.

### Idempotency

Seeders **must** be idempotent. The canonical pattern: check if any row exists; if so, exit. More sophisticated seeders check individual records and upsert only missing ones.

Non-idempotent seeders produce painful "why are there 47 copies of Alice?" bugs. Architectural test: seeders must not throw on re-invocation (verified in integration tests).

### Data quality

Seed data should reflect realistic usage:

- Multiple users with different roles
- Entities in different states (draft, active, archived)
- Data that exercises edge cases (long strings, unicode, edge-of-range numbers)
- Cross-module references where applicable

Good seed data acts as implicit end-to-end smoke test — bugs in aggregate invariants often manifest as seeder failures.

### NOT seeded

- Production data. Ever. Even as a "one-time migration." Migrations belong in `Persistence/Migrations/`.
- Secrets.
- Environment-specific operational data (feature flag states, infrastructure records).

## Consequences

**Positive:**

- First-run experience is coherent. `dotnet run` produces a usable API with example data.
- Integration tests reuse seeding logic — "given a user exists" is a fixture setup, not duplicated boilerplate.
- Per-module ownership aligns with architectural boundaries.
- `SeedEnvironment` prevents dev seed data leaking to production.

**Negative:**

- Maintenance burden — seed data evolves with the domain. Stale seeders produce invalid data and silent failures.
- Ordering between modules is author-maintained (via `Order`). Mitigated by the fact that cross-module dependencies in seed data should be rare (each module seeds its own).
- Seeders execute real domain logic (via aggregate factory methods). If the factory method's validation changes, seeders may break. Arguably a feature: stale seeders fail fast.

## Related

- ADR-0001 (Modular Monolith): per-module ownership.
- ADR-0022 (Testing Strategy): seeders reused in integration test fixtures.
