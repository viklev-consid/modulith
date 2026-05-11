# ADR-0032: TickerQ for Recurring Scheduled Jobs

## Status

Accepted

## Context

The template originally used Wolverine scheduled messages for recurring background work. That worked, but it blurred two separate concerns:

1. **When should work run?** Cron expressions, runtime toggles, job history, retries, priorities, and operator visibility.
2. **What application work should happen?** Module-owned behavior, validation, transactions, audit/cache middleware, integration events, and boundary rules.

Wolverine remains an excellent fit for the second concern. It is already the application dispatch surface through `IMessageBus`, owns the transactional outbox, and runs module handlers through a consistent middleware pipeline.

For the first concern, a dedicated scheduler adds value to the template. TickerQ provides recurring jobs, a dashboard, execution history, runtime management, and a clear cron-oriented mental model. Those are operational scheduler concerns, not domain or messaging concerns.

The main risk is introducing a second background execution model. If TickerQ jobs start containing business logic, bypassing module handlers, or reaching across module DbContexts, the scheduler becomes a boundary leak. The design must keep TickerQ narrow.

## Decision

Use TickerQ for recurring, operator-visible scheduled jobs.

Use Wolverine for the application work those jobs trigger.

The canonical pattern is:

```text
TickerQ cron trigger
  -> IMessageBus.InvokeAsync(module command)
  -> Wolverine handler
  -> module DbContext / domain behavior / middleware / outbox
```

TickerQ jobs should usually be thin orchestration classes:

```csharp
public sealed class SweepExpiredTokensJob(IMessageBus bus)
{
    public const string Name = "users.sweep-expired-tokens";
    public const string CronExpression = "0 0 3 * * *";

    [TickerFunction(Name, CronExpression)]
    public Task ExecuteAsync(TickerFunctionContext context, CancellationToken ct) =>
        bus.InvokeAsync(new SweepExpiredTokens(), ct);
}
```

Module installers expose a `ConfigureJobs(...)` hook beside `ConfigureMessaging(...)`, so job ownership remains module-local and discoverable.

The TickerQ dashboard is mounted at `/admin/jobs` and protected by the host `Admin` authorization policy.

Use Wolverine delayed/scheduled messages only for one-off follow-up work that should remain transactionally coupled to a handler. Do not use self-rescheduling Wolverine messages as the default recurring-job pattern.

## Rules

- Job names use `{module}.{job-name}` and must remain stable.
- TickerQ job classes should be thin triggers, not business-logic containers.
- Module behavior belongs in Wolverine handlers or module-owned services.
- A scheduled job may only use its owning module's DbContext directly.
- Cross-module work must use public `.Contracts` messages and `IMessageBus`.
- Recurring job handlers must be idempotent; scheduler retries and operator-triggered runs may repeat work.
- Tests should exercise behavior by invoking the Wolverine command, not by waiting for wall-clock cron.

## Consequences

**Positive:**

- Recurring jobs have first-class operational visibility through the dashboard.
- Cron schedules, runtime controls, and job history are explicit.
- Module behavior still flows through Wolverine's existing dispatch, transaction, validation, audit, cache, and outbox middleware.
- The template becomes easier to explain: TickerQ decides when; Wolverine decides what.
- The self-rescheduling-message pattern is no longer the default teaching path for recurring work.

**Negative:**

- The template now has two background execution tools, so documentation must be clear about their boundary.
- Dashboard access must be secured and treated as an admin surface.
- TickerQ package versions and persistence behavior become part of the app-level infrastructure surface.
- Teams must resist putting business orchestration directly into scheduler classes.

## Related

- ADR-0003 (Wolverine): Wolverine remains the application dispatch, outbox, delayed-message, and middleware surface.
- ADR-0005 (Module Communication): scheduled work must respect `.Contracts` boundaries.
- ADR-0012 (GDPR Primitives): retention sweeps are recurring jobs that dispatch module-owned handlers.
- ADR-0022 (Testing Strategy): test scheduled job behavior through handlers; keep wall-clock scheduler tests rare.
- ADR-0029 (Refresh Tokens): token cleanup is implemented as a TickerQ-triggered Wolverine command.
