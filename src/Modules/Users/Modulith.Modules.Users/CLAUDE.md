# CLAUDE.md — Users Module

This module owns the user identity lifecycle: registration, authentication, and profile management. It does **not** use ASP.NET Identity (see ADR-0007).

---

## Domain vocabulary

- **User** — the root aggregate. Identified by `UserId` (typed Guid).
- **Email** — a value object. Always stored lowercased. Validated on creation via `Email.Create(...)`.
- **PasswordHash** — a value object wrapping a BCrypt hash. Never log or serialize the raw value (`ToString()` returns `[REDACTED]`).
- **DisplayName** — a plain string, 1-100 characters.

---

## Invariants

1. Email addresses are unique across the `users` schema.
2. Passwords are hashed with BCrypt before the aggregate ever sees them. Plaintext passwords never enter the domain.
3. JWT tokens are signed with a symmetric key from `JwtOptions.SigningKey` (shared across modules, bound from the `Jwt:` config section). Minimum 32 characters.
4. `User.Create(...)` raises `UserRegistered` domain event. The handler publishes `UserRegisteredV1` integration event to the bus after the database write.

---

## Auth concerns

- Tokens are issued by `JwtGenerator` inside this module. The `Program.cs` JWT bearer middleware validates them.
- The `ICurrentUser` implementation reads from `HttpContext.User` claims. `ClaimTypes.NameIdentifier` holds the `UserId` (Guid string).
- `GetCurrentUser` endpoint parses the claim and queries by `UserId`. If the claim is missing or unparseable, it returns 401.

---

## Security abstractions

- `IPasswordHasher` / `BcryptPasswordHasher` — module-internal. BCrypt is infrastructure, not domain.
- `IJwtGenerator` / `JwtGenerator` — module-internal. Depends on the shared `JwtOptions` from `Shared.Infrastructure.Auth`, bound from the `Jwt:` config section by `Program.cs`. Same options drive token validation in the API pipeline, so tokens issued here are accepted there by construction.

---

## Known footguns

- Never inject `IPasswordHasher` into domain types. Hashing is infrastructure, called from handlers.
- `JwtOptions.SigningKey` must be at least 32 characters — HMAC-SHA256 requires a minimum key length. `ValidateDataAnnotations()` + `ValidateOnStart()` enforce this at startup.
- EF value converters for `Email` and `PasswordHash` — if LINQ queries against these properties behave unexpectedly, check the converter configuration in `UserConfiguration`.
