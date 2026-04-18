# ADR-0004: Result Pattern Over Exceptions

## Status

Accepted

## Context

There are two idioms for signaling operation failure in .NET:

1. **Exceptions** — throw when something goes wrong, let callers catch.
2. **Result types** — return a value that is either success-with-data or failure-with-reason.

Exceptions are idiomatic in .NET, have first-class language support, and carry stack traces. They are also:

- Invisible in method signatures (callers don't know what to catch)
- Expensive to throw and catch in hot paths
- Easy to misuse for control flow (validation errors thrown as exceptions)
- Harder to compose — chaining operations that might fail requires nested try/catch

Result types make failure explicit in the type system, compose well, and are cheap. They are less idiomatic in .NET than in F#/Rust/Go, and they require discipline about *what* counts as a failure worth returning.

## Decision

Operations that can fail for **expected reasons** return `Result<T>` (or `Result` for operations with no payload). Expected reasons include:

- Validation failures
- Business rule violations
- Not-found lookups
- Authorization failures
- Conflict/concurrency errors

Exceptions are reserved for **truly exceptional cases**:

- Bugs (null references, out-of-range indexes)
- Infrastructure faults (DB connection dead, disk full)
- Framework-level errors the app cannot sensibly recover from

Handlers return `Result<T>`. Endpoints map `Result<T>` to HTTP responses — success to 200/201 with the Response DTO, failure to a `ProblemDetails` response (ADR-0025).

### Library choice

We evaluated `FluentResults` and `ErrorOr`. The template uses **ErrorOr** because:

- More ergonomic C# API (implicit conversions, pattern matching friendly)
- Explicit error types that map cleanly to HTTP status codes via an `Error.Type` discriminator
- Active maintenance, small surface area

(If a team prefers `FluentResults`, swapping is a solution-wide refactor but not architecturally significant.)

## Consequences

**Positive:**

- Failure paths are visible in signatures. `Task<ErrorOr<Order>>` is self-documenting.
- No throwing for validation: cheaper, cleaner stack traces, easier to reason about.
- Composition is straightforward. Handlers chain `.Then(...)` or pattern-match.
- Uniform error response mapping: one place that turns `ErrorOr` failures into `ProblemDetails`.
- Tests are clearer: `result.IsError.ShouldBeTrue()` reads better than `Assert.Throws`.

**Negative:**

- Non-idiomatic in .NET, costs onboarding time. New contributors will try to throw; the arch tests and code review catch this.
- Slightly more code at call sites than unchecked exceptions. Worth it for the explicitness.
- Discipline required about exception vs. Result boundary. When a DB call throws a `DbUpdateConcurrencyException`, the handler catches it and returns `Result.Fail(Conflict(...))`. Guidelines documented in `CLAUDE.md` and `how-to/handle-failures.md`.
- Generic infrastructure code (middleware, global exception handler) still handles exceptions — they're not abolished, just de-emphasized.

## Related

- ADR-0009 (Rich Domain Model): aggregates return `Result<T>` from factory methods and state-changing operations.
- ADR-0025 (ProblemDetails): the mapping from Result failures to HTTP responses.
