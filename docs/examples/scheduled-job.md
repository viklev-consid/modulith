# Example: Recurring Scheduled Job

**Pattern:** TickerQ cron trigger dispatches a Wolverine command. TickerQ owns *when* the job runs; the module handler owns *what* the work means.

**Source:** `src/Modules/Users/Modulith.Modules.Users/Jobs/`

---

## The command

```csharp
namespace Modulith.Modules.Users.Jobs;

/// <summary>Scheduled daily to delete expired tokens beyond the grace period.</summary>
public sealed record SweepExpiredTokens;
```

`SweepExpiredTokens` is still a Wolverine message. Keeping the work behind `IMessageBus` means the job uses the same handler discovery, transaction middleware, instrumentation, and module conventions as HTTP-initiated commands.

---

## The TickerQ trigger

```csharp
using TickerQ.Utilities.Base;
using Wolverine;

namespace Modulith.Modules.Users.Jobs;

public sealed class SweepExpiredTokensJob(IMessageBus bus)
{
    public const string Name = "users.sweep-expired-tokens";
    public const string CronExpression = "0 0 3 * * *";

    [TickerFunction(Name, CronExpression)]
    public async Task ExecuteAsync(TickerFunctionContext context, CancellationToken ct)
    {
        await bus.InvokeAsync(new SweepExpiredTokens(), ct);
    }
}
```

The TickerQ job is deliberately thin. It declares the stable operator-facing job name and schedule, then dispatches the application command.

---

## The handler

```csharp
public sealed class SweepExpiredTokensHandler(UsersDbContext db, IClock clock)
{
    public async Task Handle(SweepExpiredTokens _, CancellationToken ct)
    {
        var cutoff = clock.UtcNow.AddDays(-7);

        await db.RefreshTokens
            .Where(t => t.ExpiresAt < cutoff)
            .ExecuteDeleteAsync(ct);

        await db.SingleUseTokens
            .Where(t => t.ExpiresAt < cutoff)
            .ExecuteDeleteAsync(ct);

        await db.PendingEmailChanges
            .Where(p => !db.SingleUseTokens.Any(t => t.Id == p.TokenId))
            .ExecuteDeleteAsync(ct);

        await db.PendingExternalLogins
            .Where(p => p.ExpiresAt < clock.UtcNow || p.ConsumedAt != null)
            .ExecuteDeleteAsync(ct);
    }
}
```

The handler does not reschedule itself. Recurrence belongs to TickerQ.

---

## Module registration

`UsersModuleInstaller` participates in two separate registration flows:

```csharp
public void ConfigureMessaging(WolverineOptions options)
{
    options.AddUsersHandlers();
}

public void ConfigureJobs(TickerOptionsBuilder<TimeTickerEntity, CronTickerEntity> options)
{
    options.AddUsersJobs();
}
```

Wolverine registration discovers the command handler. TickerQ discovers `[TickerFunction]` cron functions and exposes them in the jobs dashboard at `/admin/jobs`.

---

## When to use this pattern

- Recurring maintenance work such as token cleanup, retention sweeps, report generation, or consistency checks.
- Jobs operators should inspect, pause, or run from the TickerQ dashboard.
- Work that benefits from the normal module handler pipeline.

For delayed follow-up work that must be transactionally coupled to a business operation, use Wolverine delayed messages instead.

---

## Related

- [`../how-to/add-scheduled-job.md`](../how-to/add-scheduled-job.md)
- [`../how-to/cross-module-events.md`](../how-to/cross-module-events.md)
- [`../how-to/write-integration-test.md`](../how-to/write-integration-test.md)
