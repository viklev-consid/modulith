# ADR-0003: Wolverine for Messaging, Outbox, and Background Jobs

## Status

Accepted

## Context

The template needs:

1. In-process mediation (command/query dispatch).
2. Cross-module event publishing with transactional consistency (outbox pattern).
3. Background jobs and scheduled work.

Historically these are three separate libraries: MediatR for mediation, MassTransit for messaging with outbox, Hangfire or Quartz for scheduled jobs. Each has its own conventions, configuration, and learning curve. Wiring them together is a source of friction and bugs (e.g., ensuring jobs participate in the outbox, ensuring events are published after the transaction commits).

Wolverine (part of the JasperFx suite, alongside Marten) provides all three in a single library with a consistent model. It uses source generation for handler discovery (no reflection-heavy startup), integrates with EF Core for the outbox, and exposes `IMessageBus` as the single dispatch surface.

As of Wolverine 3.x, MediatR is also entering a licensing transition that makes depending on it less attractive for new greenfield projects.

## Decision

Use Wolverine for:

- In-process command and query dispatch (via `IMessageBus`)
- Cross-module integration event publishing via durable outbox, persisted through EF Core (`PersistMessagesWithEfCore`)
- Background job scheduling (`ScheduleAsync`)
- Message-pipeline middleware (validation, transactions, logging, metrics)

Configure Wolverine at the API composition root with:

- `Policies.AutoApplyTransactions()` to wrap handlers in a DB transaction per handler
- `Policies.UseDurableLocalQueues()` for local reliability
- Assembly discovery per module

Do not use MediatR, MassTransit, or Hangfire in addition.

## Consequences

**Positive:**

- One library, one mental model for three concerns. Less configuration, fewer seams to get wrong.
- Transactional outbox without custom plumbing — events published inside a handler are persisted in the same transaction as the state change.
- Source-generated handler code: fast startup, no reflection penalty.
- `TrackActivity()` test helper makes message-based assertions pleasant.
- Native scheduled message support: `bus.ScheduleAsync(msg, delay)` replaces background job libraries for most use cases.
- Middleware pipeline is explicit and discoverable — validation, audit, and caching invalidation all plug in as middleware.

**Negative:**

- Smaller ecosystem than MediatR. Fewer examples, fewer Stack Overflow answers.
- More opinionated — Wolverine has conventions about handler shapes that differ from MediatR's interface-based model. New contributors need to learn these.
- Ties the template to the JasperFx ecosystem. Not a huge risk given its maturity, but worth acknowledging.
- The outbox pattern is powerful but nonobvious. Developers need to understand that event publication is post-commit, not instantaneous.

## Related

- ADR-0002 (Vertical Slices): Wolverine's handler model aligns with slice-level `Handler.cs` files.
- ADR-0020 (No Idempotency Infrastructure): Wolverine's outbox gives at-least-once producer delivery; handler idempotency is a separate concern documented but not baked in.
- ADR-0022 (Testing Strategy): Wolverine's `TrackActivity` is the preferred way to assert published messages in integration tests.
