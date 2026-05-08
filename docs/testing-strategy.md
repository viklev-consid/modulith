# Testing Strategy

Modulith uses four test layers. Each has a distinct purpose, scope, and speed profile. The boundaries between them are strict — a test in the wrong layer is almost always a sign the test is wrong.

---

## The four layers

### 1. Unit tests — `tests/Modules/<Module>/Modulith.Modules.<Module>.UnitTests`

**Scope:** Domain only. Aggregates, value objects, domain services.

**Dependencies:** None outside the BCL and xUnit assertions. No mocking frameworks. No database. No ASP.NET.

**Speed:** Milliseconds.

**What belongs here:**
- Invariant enforcement — constructing an aggregate with invalid state returns an `ErrorOr` failure.
- State transitions — calling methods on aggregates produces the expected state and domain events.
- Value object equality and validation.

**What does NOT belong here:**
- Handlers. Handlers orchestrate I/O; they go in integration tests.
- Anything that needs a mock. If you find yourself mocking, either the domain is wrong or the test is wrong.
- Anything testing EF Core behavior.

**Why no mocks in this layer:** rich domain objects have no dependencies. If a domain test needs a mock, the domain has leaked infrastructure into itself. Treat the compile error as the lesson.

---

### 2. Architectural tests — `tests/Modulith.Architecture.Tests`

**Scope:** Structural rules across the entire solution.

**Dependencies:** NetArchTest.

**Speed:** Hundreds of milliseconds.

**What belongs here:**
- Module boundary rules (A does not reference B's internal project).
- Domain purity rules (`Domain/` folders don't reference EF Core, ASP.NET, etc.).
- Naming conventions (handlers end with `Handler`, commands are records).
- Configuration rules (no `IConfiguration` outside registration).
- Entity rules (no public setters on aggregates).
- Slice structure rules (handlers live in `Features/*/` folders).

**Failure messages must be actionable.** "Orders references Users.Persistence — move the dependency to Users.Contracts" is correct. "Rule X failed" is not. Every rule has a custom failure message explaining the fix.

See [`adr/0015-architectural-tests.md`](../docs/adr/0015-architectural-tests.md) for the full rule set.

---

### 3. Integration tests — `tests/Modules/<Module>/Modulith.Modules.<Module>.IntegrationTests`

**Scope:** One module's slice end-to-end. HTTP → handler → DB → outbox.

**Dependencies:** `WebApplicationFactory<Program>`, Testcontainers (Postgres), xUnit assertions, Respawn.

**Speed:** Seconds per test; the Testcontainers startup amortizes across the class.

**What belongs here:**
- Happy path and common failure paths for each slice.
- Validation rejection paths.
- Authorization (401, 403) checks.
- Cross-module event flows (verified with Wolverine's `TrackActivity`).
- Persistence correctness (state is what you expect after a command).
- Outbox behavior (integration events are persisted and published).

**What does NOT belong here:**
- Full-stack smoke (use smoke tests).
- Mocking the DbContext. We use real Postgres.
- "Does ASP.NET serialize JSON." Skip framework-level tautologies.

**Test class = one database.** The fixture creates a container once per class, Respawn wipes between tests. Parallel at the class level. No shared state between classes.

**Authenticated client helpers live in `TestSupport`.** Use `CreateAuthenticatedClient(userId, email, displayName, role)` for synthetic JWTs, or `CreateAuthenticatedClientWithToken(accessToken)` when the test should authenticate with a token issued by the real login/register flow. Tests do not manually craft JWTs.

**Snapshot testing is optional.** The template does not currently wire Verify; add it deliberately for response DTOs, OpenAPI documents, or published event payloads when snapshot review is worth the dependency.

**Use HTTP-level fakes for external HTTP.** The template does not currently wire WireMock.Net; if a module calls a third-party API, add a module fixture that fakes HTTP at the transport boundary.

---

### 4. Smoke tests — `tests/Modulith.SmokeTests`

**Scope:** The full API pipeline through `WebApplicationFactory`, backed by real Postgres and Mailpit containers.

**Dependencies:** `WebApplicationFactory<Program>`, Testcontainers, `HttpClient`, xUnit assertions.

**Speed:** Tens of seconds.

**What belongs here:**
- Can the API pipeline boot?
- Do the canonical happy paths work end-to-end through the real pipeline?
- Can a notification actually be sent through Mailpit and retrieved?
- Does the OpenAPI document generate and contain the expected versions?

**What does NOT belong here:**
- Business logic coverage. That's integration tests.
- Edge case coverage. That's unit + integration tests.
- Performance assertions.

Small number — 3 to 5 tests total. Runs on release branches, not every PR.

---

## Shared test infrastructure — `tests/Modulith.TestSupport`

A shared project referenced by all test projects. Current contents:

- `ApiTestFixture` — base `WebApplicationFactory<Program>` with Testcontainers.
- `CreateAnonymousClient()` / `CreateAuthenticatedClient(...)` — helpers for anonymous and JWT-backed `HttpClient` instances.
- `CreateAuthenticatedClientBuilder()` — fluent JWT client builder for tests that need custom claims.
- `CreateAuthenticatedClientWithToken(...)` — helper for tokens issued by the real auth flow.
- `QueryDbAsync<TDbContext, TResult>()` / `ExecuteDbAsync<TDbContext>()` / `SeedDbAsync<TDbContext>()` — scoped DbContext helpers.
- `TrackWolverineActivityAsync(...)` — wrapper around Wolverine `TrackActivity()` with the repo default timeout.
- `VerifyTestSettings` — shared snapshot settings that scrub volatile members.
- `WireMockFixture` — lightweight HTTP fake for module fixtures that call third-party APIs.
- `Fakes/` — fake and flaky email senders for tests that override notification delivery.
- `TestClock` — controllable time source implementing the shared `IClock` abstraction.

Without `TestSupport`, every module reinvents the harness. With it, adding a module takes an afternoon of tests, not a week.

---

## Libraries and versions

| Concern | Library | Notes |
|---|---|---|
| Test framework | xUnit v3 | Modern lifecycle, better parallelism |
| Assertions | xUnit | Current tests use built-in `Assert` APIs |
| Snapshot testing | Verify | Use `VerifyTestSettings.Create()` for shared scrubbing defaults |
| Test data | Bogus | Optional; not currently referenced by test projects |
| Database | Testcontainers (Postgres) | Real Postgres, no in-memory |
| Architectural tests | NetArchTest | Lightweight, readable |
| HTTP mocking | WireMock.Net | Use `WireMockFixture` in the module fixture that needs it |
| Message assertions | Wolverine `TrackActivity` | Assert published messages |

**Not used:**
- **AutoFixture** — fights rich domain models with private setters.
- **Moq / NSubstitute** — minimal use. Unit tests don't need mocks; integration tests use real infrastructure.
- **FluentAssertions / Shouldly** — not currently part of the template. Add an assertion library only if xUnit assertions stop carrying their weight.
- **SpecFlow / Reqnroll** — BDD overhead without BDD benefit for this use case.
- **Stryker** — mutation testing is a specialized tool; not a template default.

---

## Running tests

```bash
# Everything
dotnet test

# Fast tier only (unit + architectural)
dotnet test --filter "Category!=Integration&Category!=Smoke"

# One module's tests
dotnet test tests/Modules/Users/Modulith.Modules.Users.UnitTests
dotnet test tests/Modules/Users/Modulith.Modules.Users.IntegrationTests

# Architectural tests only
dotnet test tests/Modulith.Architecture.Tests

# Smoke tests only (slow)
dotnet test tests/Modulith.SmokeTests
```

Tests are categorized via xUnit `[Trait("Category", "Integration")]` so they can be filtered in CI.

---

## CI integration

Three tiers:

| Tier | Trigger | Duration | What runs |
|---|---|---|---|
| Fast | Every push | ~1 minute | Build + unit + architectural tests + format check |
| PR | Every PR | ~5 minutes | Everything above + integration tests |
| Release | Main / release branches | ~10 minutes | Everything above + smoke tests |

Integration tests parallelize across modules — each module's `.IntegrationTests` project is independent and runs in parallel.

---

## Writing a good test

Rules of thumb:

**Test behavior, not implementation.** "When I place an order with an invalid product, I get 400 with error code `ProductNotFound`" is behavior. "The handler calls `_productRepository.FindAsync`" is implementation — it'll change the next time someone refactors.

**One assertion per concept, many asserts per test.** A single test can verify "the order was created, the total is correct, an `OrderPlaced` event was published, and the inventory module was notified" — because those are facets of one behavior.

**Arrange-Act-Assert, explicitly.** Use blank lines or comments to separate the phases. A test without clear AAA is a test that'll confuse the next reader.

**Test names describe the scenario and the expected outcome.** `PlacingOrderWithEmptyCart_ReturnsValidationFailure` is correct. `TestPlaceOrder` is not.

**Failure messages are debugging aids.** xUnit assertion overloads are enough for most tests; when they aren't, include the relevant details in the assertion message, especially `ErrorOr` failure descriptions.

---

## Things we deliberately don't test

- That ASP.NET Core routes requests correctly.
- That FluentValidation runs when a validator is registered.
- That EF Core persists a value when you call `SaveChanges`.
- That Wolverine dispatches a command when you call `InvokeAsync`.

These are framework tautologies. We test *our* behavior on top of the framework.

---

## What to do when tests are slow

The fast tier exists to stay fast. If unit tests ever exceed ~10 seconds total, something has leaked. Likely culprits:

- A unit test doing I/O (should be integration).
- A domain class reaching infrastructure (should fail an arch test; fix the domain).
- A `[ClassInitialize]` doing work that should be per-fixture.

For integration tests, the target is ~5 minutes total for the full suite. If it grows beyond that:

- Ensure per-class database isolation (not per-test).
- Ensure Respawn is used, not DB recreation.
- Parallelize test classes.
- Move framework-level tests to unit tests where possible.

Smoke tests are allowed to be slow, but there should be few of them. Adding smoke tests for coverage is an anti-pattern.
