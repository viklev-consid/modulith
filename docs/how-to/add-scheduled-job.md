# How to Add a Scheduled Job

Recurring jobs use TickerQ for scheduling and Wolverine for application work.

The rule of thumb:

```text
TickerQ decides when work happens.
Wolverine/module handlers decide what work means.
```

Use Wolverine delayed messages instead when the scheduled work is a one-off follow-up that must be transactionally coupled to the current handler.

---

## 1. Add a command/message

Create a job folder under the owning module:

```text
src/Modules/<Module>/Modulith.Modules.<Module>/Jobs/<Name>.cs
```

Example:

```csharp
namespace Modulith.Modules.Users.Jobs;

public sealed record SweepExpiredTokens;
```

Keep the message module-owned. If another module needs to trigger it, expose a command through the owning module's `.Contracts` project instead of referencing the internal module.

---

## 2. Add the Wolverine handler

Put the behavior in a public handler:

```csharp
public sealed class SweepExpiredTokensHandler(UsersDbContext db, IClock clock)
{
    public async Task Handle(SweepExpiredTokens _, CancellationToken ct)
    {
        var cutoff = clock.UtcNow.AddDays(-7);

        await db.RefreshTokens
            .Where(t => t.ExpiresAt < cutoff)
            .ExecuteDeleteAsync(ct);
    }
}
```

Guidelines:

- Use the owning module's DbContext only.
- Use `IClock`, not `DateTimeOffset.UtcNow`.
- Prefer bulk operations such as `ExecuteDeleteAsync` for maintenance sweeps.
- Keep cross-module work behind public contracts and `IMessageBus`.
- Make the handler idempotent; recurring jobs and retries may run more than once.

Register the handler in `<Module>Module.Add<Module>Handlers(...)`:

```csharp
opts.Discovery.IncludeType<SweepExpiredTokensHandler>();
```

---

## 3. Add the TickerQ trigger

The TickerQ job should usually be orchestration only:

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

Naming convention:

```text
{module}.{job-name}
```

Examples:

```text
users.sweep-expired-tokens
audit.retention-sweep
catalog.consistency-check
```

Stable names matter because operators see them in the dashboard.

---

## 4. Register the module job extension

Each module owns its job registration point:

```csharp
public static TickerOptionsBuilder<TimeTickerEntity, CronTickerEntity> AddUsersJobs(
    this TickerOptionsBuilder<TimeTickerEntity, CronTickerEntity> opts)
{
    _ = typeof(SweepExpiredTokensJob);
    return opts;
}
```

Then call it from the module installer:

```csharp
public void ConfigureJobs(TickerOptionsBuilder<TimeTickerEntity, CronTickerEntity> options)
{
    options.AddUsersJobs();
}
```

TickerQ discovers `[TickerFunction]` methods from loaded assemblies. The module hook exists so job ownership remains visible next to `ConfigureMessaging` and `MapEndpoints`.

---

## 5. Test behavior and registration

Test the work through Wolverine:

```csharp
var bus = fixture.ApplicationHost.Services.GetRequiredService<IMessageBus>();
await bus.InvokeAsync(new SweepExpiredTokens(), CancellationToken.None);
```

Assert the database state after the handler runs. Avoid sleeping until a cron expression fires.

For scheduler-specific behavior, prefer reflection or registration checks around the job metadata. Keep real wall-clock scheduler tests rare.

---

## Operational notes

The TickerQ dashboard is mounted at:

```text
/admin/jobs
```

It uses the host `Admin` authorization policy. Local development still requires an authenticated admin user to inspect or operate jobs.
