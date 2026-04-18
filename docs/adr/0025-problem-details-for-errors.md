# ADR-0025: ProblemDetails for All Error Responses

## Status

Accepted

## Context

HTTP APIs need a consistent error response shape. Options:

1. **Ad-hoc JSON bodies** — `{ "error": "..." }`, different per endpoint. The default trajectory without discipline. Produces inconsistent client code.
2. **Custom error envelope** — a single app-specific shape. Works, but reinvents a wheel and doesn't interop with tooling.
3. **RFC 7807 ProblemDetails** — standard `application/problem+json` with `type`, `title`, `status`, `detail`, `instance`, plus extension members. Supported by `Microsoft.AspNetCore.Mvc.ProblemDetails` natively.

ProblemDetails is the right default:
- Standard, so clients and tooling (including OpenAPI generators) handle it without customization.
- Extensible — add `errorCode`, `errors`, `correlationId` as needed.
- Has specialized subtypes (`ValidationProblemDetails`) for validation scenarios.
- Integrates with ASP.NET Core's `IExceptionHandler` and `AddProblemDetails()`.

The `Result<T>` pattern (ADR-0004) pairs naturally: a `Result.Fail(Error.Validation(...))` maps to a `ValidationProblemDetails` 422; `Error.NotFound(...)` maps to a `ProblemDetails` 404; and so on.

## Decision

### All error responses use ProblemDetails

Including:

- 400 Bad Request (malformed request)
- 401 Unauthorized
- 403 Forbidden
- 404 Not Found
- 409 Conflict
- 422 Unprocessable Entity (validation failures → `ValidationProblemDetails`)
- 429 Too Many Requests
- 500 Internal Server Error

`builder.Services.AddProblemDetails()` registers the infrastructure. A custom `IExceptionHandler` (global exception handler) catches unhandled exceptions and returns a sanitized 500 `ProblemDetails` with a correlation ID.

### Mapping from Result to ProblemDetails

A central extension converts an `ErrorOr<T>` failure to an `IResult`:

```csharp
public static IResult ToProblemDetails<T>(this ErrorOr<T> result) =>
    result.Match(
        value => Results.Ok(value),
        errors => Problems.FromErrors(errors));
```

`Problems.FromErrors` inspects the error type discriminator (`Validation`, `NotFound`, `Conflict`, `Unauthorized`, `Forbidden`, `Unexpected`) and chooses the correct status code and shape.

### Stable error codes

Every error carries a stable `errorCode` string (`users.email_already_in_use`, `orders.cannot_cancel_shipped`). Surfaced in ProblemDetails as an extension member. Clients pattern-match on `errorCode`, not on `title` (which is human text and may change).

### The `type` URI

RFC 7807's `type` is a URI identifying the problem kind. The template uses `https://docs.modulith.dev/errors/{errorCode}` (or whatever the team's docs site is). The URI does not need to resolve immediately — it's an identifier — but pointing it at docs enables learn-by-URL.

### Correlation

Every ProblemDetails includes a `traceId` extension member pulled from `Activity.Current?.TraceId`. Combined with OpenTelemetry (ADR-0010), a support-bound error includes the trace ID for backend lookup.

### What NOT to do

- Don't return custom error envelopes alongside ProblemDetails — pick one, be consistent.
- Don't expose exception messages or stack traces to clients. The global exception handler sanitizes.
- Don't use 200 OK with `success: false` bodies. If it failed, return a failing status code.

## Consequences

**Positive:**

- Consistent error shape for all clients.
- Standard — OpenAPI, Scalar, client SDKs handle it without customization.
- `errorCode` is stable programmatic surface; `title`/`detail` are human-readable.
- Traceability via `traceId` simplifies support.
- Global exception handler catches bugs and returns safe messages.

**Negative:**

- Slightly verbose for trivial errors. Accepted.
- Teams used to 200-with-error-envelope need to adjust. Documented in how-to guides.
- Validation responses (422) are the one case where the extension shape matters (`errors: { field: [messages] }`); consumers must be taught to parse it.

## Related

- ADR-0004 (Result Pattern): the source of structured errors.
- ADR-0008 (FluentValidation): validation failures map to `ValidationProblemDetails`.
- ADR-0010 (Serilog/OTel): trace correlation.
- ADR-0018 (Rate Limiting): 429 responses use ProblemDetails.
