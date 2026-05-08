# AGENTS.md - Tests

This directory holds all tests. Four layers, each with strict scope. See [`docs/testing-strategy.md`](../docs/testing-strategy.md) for the full treatment; this is the operating manual.

---

## The four layers

| Layer | Location | Scope | Speed |
|---|---|---|---|
| Unit | `tests/Modules/<Module>/*.UnitTests` | Domain only | Milliseconds |
| Architectural | `tests/Modulith.Architecture.Tests` | Structural rules | Sub-second |
| Integration | `tests/Modules/<Module>/*.IntegrationTests` | One module end-to-end | Seconds per test |
| Smoke | `tests/Modulith.SmokeTests` | Full API pipeline via TestServer + real containers | Tens of seconds |

---

## What goes in each layer

### Unit tests

- Aggregate invariants: constructing with bad state -> `ErrorOr<T>` failure such as `Error.Validation(...)`.
- State transitions: method calls produce expected state + events.
- Value object validation and equality.
- Domain service logic (pure, no I/O).

**Never:**
- Mocks (rich domain has no dependencies to mock).
- DbContext, HttpClient, or any I/O.
- Handler tests (handlers orchestrate I/O - integration tests).

### Architectural tests

- Module boundaries (reference rules).
- Domain purity (no infrastructure dependencies in Domain/).
- Naming conventions (Handler suffix, `sealed record` for commands).
- No public setters on aggregates.
- No `IConfiguration` outside registration.
- No `IFeatureManager` in Domain/.

Failure messages are custom-written to be actionable. When one fails, read it literally.

### Integration tests

- Happy path for each slice.
- Common failure paths (validation, not-found, authorization).
- Cross-module event flows (assert via Wolverine `TrackActivity`).
- Persistence correctness (state after command matches expectation).
- Outbox behavior (events persisted and eventually published).

**Real Postgres via Testcontainers.** No in-memory EF. No SQLite. No mocking the DbContext.

### Smoke tests

- Does the stack boot?
- Do canonical happy paths work end-to-end?
- Does the OpenAPI document generate?
- Do notifications reach Mailpit?

3-5 tests total. Release CI only.

---

## TestSupport project

`tests/Modulith.TestSupport` is shared infrastructure. Every module test project references it. Contents:

- `ApiTestFixture` - base WebApplicationFactory + Testcontainers lifecycle.
- `CreateAnonymousClient()` / `CreateAuthenticatedClient(...)` - JWT-backed `HttpClient` helpers.
- `CreateAuthenticatedClientWithToken(...)` - uses a token issued by the real auth flow.
- `Fakes/` - fake and flaky email senders for module fixtures.
- `TestClock` - controllable `IClock` implementation.

**Do not duplicate infrastructure across modules.** If you find yourself writing a fixture that looks like another module's, move the shared part to TestSupport.

---

## Writing a good integration test

Structure:

```csharp
[Collection("OrdersModule")]
public sealed class PlaceOrderTests(OrdersApiFixture fixture) : IntegrationTestBase
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task PlacingOrderWithValidItems_CreatesOrderAndPublishesEvent()
    {
        // Arrange
        var client = fixture.CreateAuthenticatedClient(Guid.NewGuid(), "alice@example.com", "Alice");
        var request = new PlaceOrderRequest(...);

        // Act
        var response = await client.PostAsJsonAsync("/v1/orders", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<PlaceOrderResponse>();
        body.ShouldNotBeNull();

        // Assert side effects via Wolverine TrackActivity, DB queries, etc.
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        var saved = await db.Orders.FindAsync(body.OrderId);
        saved.ShouldNotBeNull();
    }
}
```

Rules of thumb:

- **One test, one scenario, clear outcome.** Multiple asserts for that scenario are fine.
- **Arrange-Act-Assert, visible.** Blank lines or comments.
- **Name describes scenario + outcome.** `PlacingOrderWithEmptyCart_ReturnsValidationFailure`.
- **Use the fixture client helpers.** Don't craft JWTs inline.
- **Use module-specific fixture helpers when they exist.** Otherwise seed through the module's real APIs or a scoped DbContext.
- **Snapshot deliberately.** The template does not currently wire Verify; add a snapshot library only when a response shape is worth reviewing as a contract artifact.

---

## Categories

Tests are tagged via `[Trait("Category", "X")]`:

- `Unit` - pure domain tests
- `Architecture` - architectural tests
- `Integration` - per-module integration tests
- `Smoke` - full-stack smoke tests

CI filters on these for tier separation.

---

## Test database lifecycle

Per class, not per test:

1. Fixture starts a Postgres container at class start.
2. Migrations run once.
3. Each test's teardown runs Respawn to wipe data (schemas preserved).
4. Fixture disposes container at class end.

Parallel at the class level. Respawn is fast; migration is slow and should happen once.

---

## When to use snapshots

- Response DTOs (snapshot the shape; diff on changes).
- Published events (snapshot the payload).
- Generated OpenAPI document (contract regression).

Not for:
- Assertions on business correctness (use ordinary xUnit assertions).
- Volatile fields (timestamps, IDs) unless your snapshot setup scrubs them.

---

## Mocking rules

- **Unit layer: no mocks, ever.**
- **Integration layer: no mocks for internal infrastructure (DbContext, IMessageBus).** Real Postgres, real Wolverine.
- **Integration layer: fake external HTTP at the transport boundary.** Add WireMock.Net or a similar fixture in the module that needs it.
- **Integration layer: TestClock for time when you need to control it.**

`Moq` or `NSubstitute` are not forbidden outright - occasionally a handler has a genuine external dependency with no HTTP surface. But reach for real infrastructure first.

---

## Common pitfalls

- **Flaky tests from time.** Use `TestClock`, not `DateTime.UtcNow`.
- **Flaky tests from port conflicts.** Testcontainers assigns random ports; don't hardcode.
- **Slow tests from excessive setup.** One fixture per class, not per test.
- **Tests that test the framework.** "Does EF save when I call SaveChanges" is not a test.
- **Tests that duplicate the handler's logic.** Test the *behavior*, not the *implementation*.
- **Snapshot tests that drift unnoticed.** Snapshot diffs go through PR review, not auto-acceptance.

---

## What to do when tests fail

1. Read the failure message. xUnit's assertion output is usually enough.
2. For architectural tests, read the failure literally - it names the fix.
3. For integration tests that pass locally and fail in CI: check test isolation (leaked state), time sensitivity, or port conflicts.
4. For flaky tests: flakiness is a bug. Don't rerun and move on. Investigate.

---

## What to do when tests are slow

See `docs/testing-strategy.md` for the targets. If the fast tier exceeds 1 minute or the PR tier exceeds 5 minutes:

- Check for I/O in unit tests.
- Check for DB-recreation (vs. Respawn) in integration tests.
- Check for non-class-scoped fixtures.
- Consider whether the test really needs integration scope.
