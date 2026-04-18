# ADR-0007: No ASP.NET Identity — Lightweight Custom User Aggregate

## Status

Accepted

## Context

ASP.NET Identity is the out-of-the-box solution for user management in ASP.NET Core. It provides user storage, password hashing, roles, claims, lockout, 2FA, email confirmation, and external login integration.

It also:

- Ships a fixed set of entities (`IdentityUser`, `IdentityRole`, etc.) with anemic models and public setters on every field, conflicting with the rich-domain decision (ADR-0009).
- Couples the user model tightly to EF Core Identity-specific configuration.
- Brings ~12 tables by default.
- Assumes authentication concerns live inside the application. In reality, many modern apps use external identity providers (Auth0, Entra, Keycloak, Cognito).
- Is difficult to customize past superficial levels — rich behavior (typed IDs, value objects for email, domain events on user changes) fights the framework.

For a general-purpose template, the common case is: an application that may or may not use an external IdP, that needs JWT validation, that has its own user aggregate with domain-specific attributes and invariants.

## Decision

Do not use ASP.NET Identity. Instead:

1. **Authentication is JWT bearer.** The API validates tokens via `Microsoft.AspNetCore.Authentication.JwtBearer`. Symmetric signing key for dev (auto-generated and persisted to user-secrets); asymmetric (RSA/ECDSA) recommended for prod.
2. **The Users module owns a rich `User` aggregate.** With a strongly-typed `UserId`, value objects for `Email`, `HashedPassword`, etc., and methods like `ChangeEmail(...)` that enforce invariants and raise domain events.
3. **Role/permission data is the Users module's responsibility.** A simple `Role` entity and `UserRole` join is enough. Authorization policies in the API layer map roles to policies.
4. **Password hashing via BCrypt.Net-Next** (or Argon2id if the team prefers). The `User` aggregate owns password verification and change logic.
5. **External IdPs are an extension point.** If a team swaps JWT-issuer from internal to external, they replace the login slice and the token endpoint. The rest of the app continues to validate bearer tokens the same way.
6. **Change history for user-critical events** (login, role change, email change) flows through the Audit module (ADR-0011).

## Consequences

**Positive:**

- `User` is a rich domain entity, consistent with ADR-0009.
- The Users module is smaller and more understandable than ASP.NET Identity's 12 tables.
- Swapping to an external IdP is a local change, not a rewrite.
- No dependency on the Identity framework evolution.

**Negative:**

- More code to write and maintain: login, password reset, email confirmation, 2FA (if needed) are the team's responsibility.
- Some security concerns (timing attacks on login, lockout policies, breach-password checks) need explicit implementation. Mitigated by documentation in the Users module.
- Teams who explicitly want Identity have to remove the custom module and add Identity themselves.

## Consequences for the template

The Users module in the template ships with:

- `User` aggregate with `Email`, `HashedPassword`, `Role`s
- Slices for `Register`, `Login`, `ChangeEmail`, `ChangePassword`, `RequestPasswordReset`, `ResetPassword`
- `Login` slice issues JWTs via an `ITokenIssuer` seam (`ISigningKeyProvider` is the injectable for key source)
- User change history via the Audit module
- Does NOT ship with: 2FA, email confirmation (hook documented), external login integration, breach-password checks

Features not shipped are documented as extension points.

## Related

- ADR-0009 (Rich Domain Model): the `User` aggregate is a primary example.
- ADR-0011 (Auditing Strategy): user change history is a compliance concern covered here.
- ADR-0021 (Config and Secrets): JWT signing keys are secrets managed per environment.
