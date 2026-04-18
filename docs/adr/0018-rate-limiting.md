# ADR-0018: Built-in Rate Limiting with Tiered Policies

## Status

Accepted

## Context

Rate limiting is standard for any public API. Options:

1. **Roll your own.** Don't.
2. **Third-party libraries** (AspNetCoreRateLimit, etc.). Mature but older and less well-maintained since the built-in option shipped.
3. **ASP.NET Core built-in** (`Microsoft.AspNetCore.RateLimiting`, GA since .NET 7). Well-integrated, policy-based, no extra dependencies.
4. **Ingress-level limiting** (API gateway, Cloudflare, nginx). The right answer for production at scale, but doesn't help in dev or in single-instance deployments.

For a template, the right combination is **built-in middleware for application-aware limits** (per-user, per-endpoint), with documentation pointing to **ingress-level limiting for distributed protection**.

A critical constraint: the built-in middleware is **in-memory**. In a multi-instance deployment, limits are per-instance. Implementing a distributed limiter requires Redis plumbing that is deploymen-specific and does not belong in the template.

## Decision

Use the built-in ASP.NET Core rate limiting middleware. Define **tiered policies** applied per-endpoint:

### Policy tiers

| Policy | Use case | Window | Limit |
|---|---|---|---|
| `auth` | Login, password reset, token endpoints | Sliding, 1 min | 5 per IP |
| `write` | POST/PUT/DELETE operations | Fixed, 1 min | 60 per user |
| `read` | GET operations | Fixed, 1 min | 300 per user |
| `expensive` | Reports, exports, heavy aggregations | Fixed, 1 min | 10 per user |
| `global` | Fallback for anything unmarked | Fixed, 1 min | 1000 per IP |

Endpoints opt in by attribute: `.RequireRateLimiting("write")`. The `global` policy applies as the fallback to anything unmarked.

### Partition keys

- **Authenticated requests**: partition by user ID.
- **Unauthenticated requests**: partition by client IP.
- **Auth endpoints specifically**: partition by IP even if authenticated (prevents an attacker with a valid token from hitting login on behalf of someone else).

### Exemptions

The following are exempted from rate limiting (marked `.DisableRateLimiting()`):

- Health check endpoints (`/health`, `/health/ready`, `/health/live`)
- Metrics endpoints (`/metrics` if exposed)
- OpenAPI document endpoint (`/openapi/v1.json`)
- Scalar UI static assets

### Response

When rate-limited, the middleware returns:

- `429 Too Many Requests`
- `Retry-After` header with seconds until reset
- `ProblemDetails` body with a stable error code (`rate_limit_exceeded`) for programmatic handling
- A structured log at `Warning` level, with user ID (if any), IP, endpoint, and policy name — for observability

### Distributed rate limiting

**Not implemented in the template.** Documented in `CLAUDE.md` and in the deployment guide:

> The built-in rate limiter is in-memory. In multi-instance deployments, limits are per-instance. For distributed rate limiting, put limiting at the ingress layer (API gateway, Cloudflare, nginx `limit_req`). Building a Redis-backed `PartitionedRateLimiter` is possible but distraction-heavy for the template's purposes.

## Consequences

**Positive:**

- No external library.
- Policies are named and centralized; endpoints opt in by name, which scans easily in code review.
- Response is RFC-consistent (`429` + `Retry-After` + `ProblemDetails`).
- Log/metric events on rejection make abuse observable.

**Negative:**

- Single-instance limiting only. Multi-instance deployments get N × the limits. Documented.
- No per-user overrides out of the box (e.g., "premium users get 2x limits"). Extension point is a custom `PartitionedRateLimiter`; not shipped.
- Test coverage for rate limits is awkward — integration tests that intentionally exceed limits are slow; usually one smoke test verifies the middleware is wired up, unit-level policy rules go untested.

## Related

- ADR-0025 (ProblemDetails): 429 responses conform to the standard shape.
