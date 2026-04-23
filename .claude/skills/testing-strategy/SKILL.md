---
name: testing-strategy
description: Guide for choosing the correct test layer in Modulith and writing unit, architecture, integration, and smoke tests with the repo's shared test infrastructure.
---

# Testing Strategy

Use this skill when you are deciding what kind of test to write or modifying tests in `tests/`.

Typical triggers:

- adding tests for a new slice
- deciding whether something belongs in unit or integration scope
- asserting message publication or subscriber side effects
- reviewing slow or flaky tests

Do not use this skill when:

- the task is only production code and no test decision is needed
- the task is only changing infrastructure unrelated to the test harness

## Read first

Before writing tests, read:

1. `/tests/CLAUDE.md`
2. `docs/testing-strategy.md`
3. one nearby test class in the same module
4. `docs/how-to/write-integration-test.md` if you need a new integration fixture pattern

## Choose the layer first

Every test belongs to exactly one of four layers.

### Unit tests

Location:

- `tests/Modules/<Module>/Modulith.Modules.<Module>.UnitTests`

Use for:

- aggregate invariants
- value object validation and equality
- domain state transitions
- internal domain event emission

Do not use for:

- handlers
- DbContext interactions
- HTTP endpoints
- message bus behavior

Rule: no mocks in this layer.

### Architectural tests

Location:

- `tests/Modulith.Architecture.Tests`

Use for:

- boundary rules
- naming conventions
- domain purity
- configuration rules

When one fails, read the message literally. The failure text is part of the documentation.

### Integration tests

Location:

- `tests/Modules/<Module>/Modulith.Modules.<Module>.IntegrationTests`

Use for:

- feature slice happy paths
- common failure paths
- persistence correctness
- authorization outcomes
- outbox behavior
- cross-module subscriber flows

This is the default layer for handlers. Do not write handler unit tests.

### Smoke tests

Location:

- `tests/Modulith.SmokeTests`

Use for:

- full Aspire stack boot
- a few canonical end-to-end workflows
- OpenAPI generation
- infrastructure-level sanity checks such as Mailpit delivery

Keep smoke tests few.

## Default decision rules

Use these shortcuts.

- aggregate or value object behavior -> unit test
- handler behavior -> integration test
- endpoint + DB + bus flow -> integration test
- cross-module event cascade -> integration test with `TrackActivity()`
- structural rule -> architecture test
- full stack boot and one real flow -> smoke test

## Integration test rules

Integration tests use the real application pipeline.

Required characteristics:

- real Postgres via Testcontainers
- real Wolverine pipeline
- `WebApplicationFactory<Program>` via shared fixtures
- per-class fixture reuse
- database reset between tests, not full recreation per test

Do not mock:

- the module DbContext
- `IMessageBus`
- internal infrastructure already provided by the host

Use the shared test support project instead of inventing local harness code.

## Shared test infrastructure

Prefer the shared helpers already in `tests/Modulith.TestSupport`:

- `ApiTestFixture`
- `AuthenticatedClientBuilder`
- object mothers and test data builders
- `WireMockFixture`
- `TestClock`
- shared Verify settings

If a helper would be useful to more than one module, put it in `TestSupport` instead of copying it.

## Message flow testing

When the behavior publishes or cascades messages:

- use Wolverine `TrackActivity()`
- wait for the cascading message flow to complete
- assert both the published envelope and the side effect

Do not use sleeps or polling loops when `TrackActivity()` can prove the flow.

## Time and external dependency rules

Use `TestClock` when time matters.

Use WireMock.Net when the application calls third-party HTTP services.

Do not hardcode ports or wall-clock assumptions.

## Snapshot testing rules

Use Verify when the contract shape matters.

Good Verify targets:

- response DTOs
- public event payloads
- OpenAPI documents

Do not use snapshots for core business invariants that should be asserted explicitly with Shouldly.

## Naming and structure rules

Each test should have:

- one clear scenario
- one clear outcome
- explicit Arrange, Act, Assert structure
- category attributes matching the layer

Prefer names such as:

- `PlacingOrderWithEmptyCart_ReturnsValidationFailure`
- `ChangingEmail_PublishesUserEmailChangedV1`

Avoid generic names such as `TestCreateUser`.

## Flakiness rules

Flaky tests are bugs.

Common causes:

- time sensitivity without `TestClock`
- leaked state between tests
- port assumptions
- manual waiting instead of message tracking
- per-test container or host setup when per-class setup would do

Fix the source of flakiness. Do not just rerun until green.

## Speed rules

If tests are slow, first check whether they are in the wrong layer.

Typical mistakes:

- unit tests doing I/O
- integration tests recreating the database every time
- smoke-test behavior duplicated across many tests
- unnecessary fixture setup per test

## Common mistakes

Avoid these:

- unit-testing handlers
- mocking the DbContext in integration tests
- asserting framework tautologies such as JSON serialization defaults
- writing tests against private implementation details
- using full-stack smoke tests for business logic coverage
- duplicating fixture infrastructure inside a single module

## Definition of done

A testing change is complete when:

- the test lives in the correct layer
- the scenario is behavior-focused
- integration tests use the shared harness and real infrastructure
- time and external calls are controlled with the shared helpers
- message flows use `TrackActivity()` when relevant
- the test category matches the layer

## Reference material

Use these as the source of truth:

- `/tests/CLAUDE.md`
- `docs/testing-strategy.md`
- `docs/how-to/write-integration-test.md`
- `docs/how-to/cross-module-events.md`
