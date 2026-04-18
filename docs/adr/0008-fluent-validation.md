# ADR-0008: FluentValidation for Request Validation

## Status

Accepted

## Context

Request validation in ASP.NET Core has three common approaches:

1. **DataAnnotations attributes** — `[Required]`, `[MaxLength(50)]`, etc. Built-in, simple, but limited (no cross-field validation, clumsy for conditional rules) and clutters DTOs.
2. **FluentValidation** — external library with a fluent DSL. More expressive, separates validation from DTO, testable in isolation.
3. **Manual validation in handlers** — write the checks in the handler. Most flexible, least reusable.

For a template aimed at general-purpose API development, attribute-based validation hits its limits early. Manual validation scatters rules across handlers. FluentValidation is the de facto standard for anything beyond trivial cases.

## Decision

Use **FluentValidation** for request-level validation. Each slice has a `{Feature}.Validator.cs` that validates the `Request` DTO (or the `Command` if they differ meaningfully).

Validators run as Wolverine middleware before the handler. Validation failures short-circuit with a `Result.Fail(Error.Validation(...))`, which the endpoint translates to an RFC 7807 `ValidationProblemDetails` response (400 Bad Request with per-field errors).

**Scope of request-level validation:**

- Required/optional field checks
- Format (email, URL, UUID)
- Length and range bounds
- Structural constraints (array non-empty, etc.)
- Cross-field rules (start date before end date)
- Light referential checks (the referenced UserId exists — when cheap)

**NOT in request-level validation:**

- Business invariants ("can't cancel a shipped order") — these live in the aggregate.
- Authorization — that's the pipeline's job, not validation's.
- Side-effectful checks — no database writes, no external API calls.

## Consequences

**Positive:**

- DTOs stay clean — no validation attributes polluting them.
- Validators are unit-testable in isolation.
- Conditional and cross-field rules are expressible and readable.
- The validator file per slice is consistent with the vertical slice shape (ADR-0002).
- Integration with Wolverine middleware means every handler is validated without per-handler plumbing.

**Negative:**

- External dependency.
- Some teams find the fluent DSL verbose for simple cases.
- Two layers of validation (request + domain). This is intentional — request validation catches shape/format; domain validation catches invariants. But developers must understand the split.

## Related

- ADR-0002 (Vertical Slices): validator lives in the slice folder.
- ADR-0004 (Result Pattern): validation failures are Results, not exceptions.
- ADR-0009 (Rich Domain Model): domain invariants are NOT a FluentValidation concern.
- ADR-0025 (ProblemDetails): validation failures map to `ValidationProblemDetails`.
