# ADR-0022: Four-Layer Testing Strategy

## Status

Accepted

## Context

Test strategy in a modular monolith has to resolve two tensions:

1. **Breadth vs. speed.** Comprehensive coverage costs wall-clock time; fast feedback requires tight scopes.
2. **Real vs. fake dependencies.** Real databases catch bugs that in-memory fakes miss; fakes run faster but teach false lessons.

A single uniform test layer is always wrong. Unit-only tests miss integration bugs; integration-only tests are slow and fragile; mixing them ad-hoc produces a suite that is neither fast nor reliable.

## Decision

Four test layers with strict scope boundaries. See `docs/testing-strategy.md` for full details; this ADR records the decision.

### Layer 1: Unit tests

- **Scope**: Domain only — aggregates, value objects, domain services.
- **Dependencies**: BCL + Shouldly. No mocks, no database, no ASP.NET.
- **Location**: `tests/Modules/<Module>/Modulith.Modules.<Module>.UnitTests`.
- **Speed**: Milliseconds total for the layer.

No mocks because rich domain objects have no dependencies to mock. If a mock is needed, the test or the domain is wrong.

### Layer 2: Architectural tests

- **Scope**: Solution-wide structural rules.
- **Dependencies**: NetArchTest.
- **Location**: `tests/Modulith.Architecture.Tests`.
- **Speed**: Hundreds of milliseconds.

Enforces ADR-0005, 0009, 0015, 0021, 0019. Failure messages are custom-written to be actionable (ADR-0027).

### Layer 3: Integration tests

- **Scope**: One module's slices end-to-end — HTTP → handler → DB → outbox.
- **Dependencies**: `WebApplicationFactory<Program>`, Testcontainers (real Postgres), Shouldly, Verify, Bogus, WireMock.Net.
- **Location**: `tests/Modules/<Module>/Modulith.Modules.<Module>.IntegrationTests`.
- **Speed**: Seconds per test, amortized across fixtures. Whole-suite target: under 5 minutes.

**Real Postgres via Testcontainers.** No in-memory EF, no SQLite substitute. In-memory EF has different semantics (no real SQL, no constraints, no real transactions) and teaches wrong things.

Database-per-test-class, Respawn between tests within the class. Class-level parallelism.

### Layer 4: Smoke tests

- **Scope**: Full Aspire stack, real HTTP.
- **Dependencies**: `Aspire.Hosting.Testing`.
- **Location**: `tests/Modulith.SmokeTests`.
- **Speed**: Tens of seconds. 3-5 tests total.

Only runs in release CI, not on every PR.

### Shared infrastructure: TestSupport

A single `tests/Modulith.TestSupport` project with:

- `ApiTestFixture` base (WebApplicationFactory + Testcontainers)
- `AuthenticatedClientBuilder` (JWT-backed HttpClient builder)
- Test data builders / object mothers
- Shared Verify settings
- WireMock helpers
- `TestClock` for controllable time

### Library choices

| Concern | Library | Rationale |
|---|---|---|
| Test framework | xUnit v3 | Modern fixture lifecycle, parallel semantics |
| Assertions | Shouldly | Readable, stable API, no license concerns |
| Snapshots | Verify | For contract shapes, OpenAPI, events |
| Fakes | Bogus | Simple; object mothers for domain |
| DB | Testcontainers (Postgres) | Real behavior, no surprises |
| Arch tests | NetArchTest | Readable syntax; rules ARE documentation |
| HTTP mocking | WireMock.Net | For external API calls |

**Not used:**
- AutoFixture — fights private setters in rich domain.
- Moq/NSubstitute — minimal need; unit layer has no mocks, integration uses real infra.
- FluentAssertions — license changes in v8 made it a maintenance risk.
- SpecFlow/Reqnroll — BDD overhead without the benefit for this use case.
- Stryker — specialized tool; not a default.

### CI tiers

| Tier | Trigger | Runs |
|---|---|---|
| Fast | Every push | Build + unit + architectural |
| PR | Every PR | Fast + integration |
| Release | Main / release branches | PR + smoke |

## What we deliberately don't test

- That ASP.NET Core routes.
- That FluentValidation runs when registered.
- That EF Core persists on `SaveChanges`.
- That Wolverine dispatches on `InvokeAsync`.

These are framework tautologies. Test our behavior *on top of* the framework.

## Consequences

**Positive:**

- Fast feedback at the right layer. Boundary bugs caught in a minute; integration bugs caught in five.
- Clear rules about what goes where — reduces debate in PRs.
- Real DB means no "works in tests, fails in prod" class of bugs.
- Per-module test projects parallelize cleanly.

**Negative:**

- Four projects per module in the solution (module + contracts + unit tests + integration tests). Accepted.
- Testcontainers requires Docker running locally. Documented in README.
- Snapshot tests (Verify) require maintenance when contracts change — intentional, but a habit to form.

## Related

- ADR-0015 (Architectural Tests): enforced by Layer 2.
- ADR-0027 (Agentic Development): test failure quality is an agent concern.
