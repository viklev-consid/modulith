---
name: wolverine-messaging
description: Repo-specific guidance for using Wolverine in Modulith. Covers IMessageBus dispatch, handler registration, outbox semantics, subscriber design, scheduled jobs, and testing with TrackActivity.
---

# Wolverine Messaging

Use this skill when a change involves Wolverine message dispatch, handler discovery, outbox publication, subscribers, or scheduled background work.

Typical triggers:

- endpoint to handler dispatch through `IMessageBus`
- publishing integration events
- adding subscriber handlers in `Integration/`
- adding a scheduled message or background job
- testing message flows

Do not use this skill when:

- the task is primarily deciding module boundaries
- the task is purely domain modeling with no bus behavior
- the task requires changing Wolverine root configuration without explicit instruction

## Read first

Before changing Wolverine usage, read:

1. `/CLAUDE.md`
2. `/src/Modules/CLAUDE.md`
3. `docs/adr/0003-wolverine-for-messaging.md`
4. `docs/how-to/cross-module-events.md`
5. `docs/how-to/add-idempotency.md`
6. `docs/examples/scheduled-job.md`
7. one nearby module's `<Module>Module.cs`

## The single dispatch surface

In this repo, `IMessageBus` is the application dispatch surface.

Use it for:

- command dispatch with `InvokeAsync<T>()`
- query dispatch with `InvokeAsync<T>()`
- integration event publication with `PublishAsync(...)`
- scheduled work via scheduled message publication

Do not introduce MediatR, MassTransit, or Hangfire alongside Wolverine.

## Endpoint dispatch rules

Endpoints should:

- construct a command or query from HTTP input
- call `bus.InvokeAsync<ErrorOr<TResponse>>(...)`
- map the result to HTTP

Endpoints must not call handlers directly.

## Handler shape rules

Wolverine handlers in this repo are concrete classes with `Handle(...)` methods.

Required rules:

- handler classes must be `public`
- handlers should usually be `sealed`
- feature handlers live next to their slice
- subscriber handlers live in `Integration/`

Important: `internal` handler classes compile but fail at runtime with Wolverine discovery errors.

## Handler registration rules

This repo uses explicit module-level discovery registration.

When adding a handler, update `<Module>Module.cs`:

```csharp
public static WolverineOptions AddCatalogHandlers(this WolverineOptions opts)
{
    opts.Discovery.IncludeType<CreateProductHandler>();
    return opts;
}
```

Do this for:

- feature handlers
- integration subscribers
- internal publisher handlers that map domain events to integration events
- scheduled job handlers

If the handler is not included, the behavior is not wired.

## Outbox rules

Wolverine provides the durable outbox and transactional publication behavior.

Key implications:

- write handlers save state first
- then they call `bus.PublishAsync(...)`
- the outgoing message is persisted transactionally
- subscribers observe it after the transaction commits

Do not design code that assumes a subscriber runs before the publisher's transaction completes.

## Event publication rules

When other modules care about a state change:

- raise an internal domain event from the aggregate if the change matters inside the module
- map it to a public integration event in `.Contracts/Events`
- publish the public event via Wolverine

Public event rules:

- version suffix such as `V1`
- primitives and DTOs only
- no domain types

## Idempotency rules

The repo does not ship built-in idempotency infrastructure.

Follow these conventions instead:

- prefer naturally idempotent subscriber logic
- use state-based operations where possible
- add explicit dedup only when the use case needs it
- assume consumers may see the same message twice

The outbox guarantees at-least-once producer delivery. It does not make subscribers idempotent.

## Scheduled job rules

The repo's documented background-job pattern is a scheduled message, often self-rescheduling.

Prefer nearby examples over inventing new abstractions.

Current documented pattern:

- a record message acts as the trigger
- the handler does the maintenance work
- it publishes the next scheduled occurrence using Wolverine delivery options

If you need a more complex workflow such as a saga or state machine, stop and ask first. This repo does not yet codify a canonical saga pattern.

## Testing rules

When testing Wolverine behavior:

- use integration tests, not handler unit tests
- use `TrackActivity()` for cascaded message flows
- assert both the published envelope and the resulting state change
- avoid sleeps and polling

If the change is only local command or query dispatch with no cross-module effects, normal slice integration tests are still enough.

## Common mistakes

Avoid these:

- making handler classes `internal`
- forgetting `IncludeType<...>()` registration in `<Module>Module.cs`
- assuming `PublishAsync(...)` means inline subscriber execution
- publishing internal domain events directly as public contracts
- adding MediatR-style abstractions on top of Wolverine
- relying on the outbox as if it solved consumer idempotency
- inventing a saga pattern that the repo has not standardized

## Ask-first cases

Stop and ask before proceeding if:

- the change requires touching Wolverine root configuration in composition
- the change introduces a new transport or durability model
- the change needs a saga or orchestrator pattern not already present in the repo

## Definition of done

A Wolverine-related change is complete when:

- endpoints dispatch through `IMessageBus`
- new handlers are `public` and explicitly registered
- published integration events are versioned and contract-safe
- outbox timing assumptions are correct
- integration tests cover the message flow where relevant

## Reference material

Use these as the source of truth:

- `docs/adr/0003-wolverine-for-messaging.md`
- `docs/how-to/cross-module-events.md`
- `docs/how-to/add-idempotency.md`
- `docs/examples/scheduled-job.md`
- `/CLAUDE.md`
- `/src/Modules/CLAUDE.md`