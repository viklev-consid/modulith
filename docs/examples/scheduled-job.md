# Example: Scheduled Background Job

**Pattern:** Wolverine message that self-reschedules — runs once daily, re-enqueues itself for the next day.

**Source:** `src/Modules/Users/Modulith.Modules.Users/Jobs/SweepExpiredTokensHandler.cs`

---

## The job

```csharp
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Users.Persistence;
using Modulith.Shared.Kernel.Interfaces;
using Wolverine;

namespace Modulith.Modules.Users.Jobs;

/// <summary>Scheduled daily to delete expired tokens beyond the grace period.</summary>
public sealed record SweepExpiredTokens;        // message trigger — no payload needed

public sealed class SweepExpiredTokensHandler(UsersDbContext db, IClock clock, IMessageBus bus)
{
    public async Task Handle(SweepExpiredTokens _, CancellationToken ct)
    {
        // Retain tokens for 7 days past expiry for audit/forensics — then hard delete
        var cutoff = clock.UtcNow.AddDays(-7);

        await db.RefreshTokens
            .Where(t => t.ExpiresAt < cutoff)
            .ExecuteDeleteAsync(ct);

        await db.SingleUseTokens
            .Where(t => t.ExpiresAt < cutoff)
            .ExecuteDeleteAsync(ct);

        // Re-schedule for next day
        await bus.PublishAsync(
            new SweepExpiredTokens(),
            new DeliveryOptions { ScheduledTime = clock.UtcNow.AddDays(1) });
    }
}
```

---

## Anatomy

### Message trigger

`SweepExpiredTokens` is a plain record — no payload, no return value. Wolverine matches it to the handler by convention.

### `ExecuteDeleteAsync`

Bulk-delete via EF Core's `ExecuteDeleteAsync` — translates to a single `DELETE WHERE` SQL statement. Never load entities into memory just to delete them in a background sweep.

### Self-reschedule

The handler publishes the next invocation before returning. `DeliveryOptions.ScheduledTime` tells Wolverine to enqueue the message for delivery at that time. The message goes through the durable outbox — it survives process restarts.

Because the handler runs inside a Wolverine message transaction, the reschedule and the deletes are atomic: if the transaction rolls back, neither happens and the next scheduled delivery is the one already in the queue.

### `IClock` instead of `DateTime.UtcNow`

Always use `IClock.UtcNow`. Integration tests inject `TestClock` to control time and verify the sweep fires correctly at the boundary conditions.

### Handler visibility

`public sealed class` — Wolverine requires it.

---

## Seeding the first run

Register the initial schedule in the module seeder so it fires on the first deployment:

```csharp
// src/Modules/Users/Modulith.Modules.Users/Seeding/UsersModuleSeeder.cs
public sealed class UsersModuleSeeder(IMessageBus bus, IClock clock) : IModuleSeeder
{
    public async Task SeedAsync(CancellationToken ct)
    {
        // Seed users ...

        // Kick off the daily token sweep if not already scheduled
        await bus.PublishAsync(
            new SweepExpiredTokens(),
            new DeliveryOptions { ScheduledTime = clock.UtcNow.AddDays(1) });
    }
}
```

The sweep already reschedules itself after each run, so the seeder only needs to fire once. On re-deployment the job is already in the durable queue.

---

## When to use this pattern

- Periodic maintenance tasks (token sweep, log rotation, report generation).
- Tasks that must survive restarts — the durable outbox ensures delivery.
- Tasks with simple recurrence logic — for complex cron-style schedules, prefer a dedicated Wolverine `ScheduledJob`.

---

## Related

- [`../adr/0003-wolverine-for-messaging.md`](../adr/0003-wolverine-for-messaging.md)
- [`../adr/0026-module-seeders.md`](../adr/0026-module-seeders.md)
- [`../how-to/write-integration-test.md`](../how-to/write-integration-test.md)
