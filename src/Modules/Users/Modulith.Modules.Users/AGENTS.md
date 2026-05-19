# AGENTS.md - Users Module

This module owns the user identity lifecycle: registration, username/password authentication, profile management, RBAC, and GDPR user data handling. It does not use ASP.NET Identity; see ADR-0007.

For general module conventions, see [`../../AGENTS.md`](../../AGENTS.md).

---

## Domain vocabulary

- **User** - the root aggregate. Identified by `UserId` (typed Guid).
- **Email** - value object, always lowercased and validated through `Email.Create(...)`.
- **PasswordHash** - value object wrapping a BCrypt hash. Never log or serialize the raw value; `ToString()` returns `[REDACTED]`.
- **SingleUseToken** - tokenized primitive for password reset and email change. Stored as SHA-256 hash; raw value exists only in transit.
- **RefreshToken** - session entity. Stored hashed and rotated on each use.
- **PendingEmailChange** - requested-but-unconfirmed new email plus token hash.
- **Consent** - tracks user agreement to processing purposes.
- **TermsAcceptance** - immutable record of a legal document version accepted by a user.
- **TwoFactorCredential** - optional per-user TOTP credential; the protected secret uses ASP.NET Core Data Protection.
- **RecoveryCode** - hashed, single-use fallback code for 2FA login and disable flows.
- **PendingTwoFactorChallenge** - short-lived, hashed login challenge issued after first-factor success when local 2FA is enabled.

---

## Shipped flows

- Register, login, refresh token rotation, logout, logout all sessions.
- Get current user, list users, get user by ID, change user role.
- Forgot password, reset password, change password.
- Request and confirm email change.
- Consent tracking, GDPR export, GDPR erasure.
- Optional per-user TOTP two-factor authentication with recovery codes.

---

## Invariants

1. Email addresses are unique across the `users` schema and stored lowercased.
2. Passwords are BCrypt-hashed before the aggregate ever sees them. Plaintext passwords never enter the domain.
3. Token values (`SingleUseToken`, `RefreshToken`, pending 2FA challenge token, recovery codes) are stored as SHA-256 hashes. Raw values exist only in HTTP responses to Notifications, email bodies, or one-time 2FA setup/regeneration responses.
4. Enumeration is not possible through forgot-password or request-email-change. Keep responses uniform.
5. Security-sensitive events revoke refresh tokens: password reset revokes all; password change revokes all except the current session; email change confirmation revokes all.
6. Refresh-token reuse triggers full chain revocation and forced re-login.
7. Use `IClock`; never use `DateTime.UtcNow` in this module.
8. TOTP secrets are protected with ASP.NET Core Data Protection; production deployments must persist and share the key ring.
9. Pending 2FA challenges expire, have capped failed attempts, and use opaque lockout errors.

---

## Auth and authorization

- JWT validation configuration lives in shared `JwtOptions` (`Shared.Infrastructure.Auth`) and is bound from `Jwt:` by the API composition root.
- Access tokens are issued by `JwtGenerator` inside this module using the shared signing options.
- Refresh token lifetime and session caps live in `UsersOptions`; refresh-token lifecycle stays in Users.
- `ICurrentUser` reads `ClaimTypes.NameIdentifier` as the `UserId` plus the active refresh-token ID claim.
- Permission constants live in `Users.Contracts/Authorization/UsersPermissions.cs`.
- `PermissionCatalog` discovers `*Permissions` types from `.Contracts` assemblies.
- Role changes have a stale-permission window equal to the access token lifetime. This is the accepted stateless-JWT tradeoff from ADR-0030.

---

## Security abstractions

- `IPasswordHasher` / `BcryptPasswordHasher` - infrastructure, called from handlers. Never inject into domain types.
- `IJwtGenerator` / `JwtGenerator` - issues access tokens.
- `IRefreshTokenIssuer` - issues and rotates `RefreshToken` entities.
- `ISingleUseTokenService` - creates and verifies single-use token primitives.
- `ITotpService` / `TotpService` - generates TOTP secrets and verifies 6-digit codes against the current step plus a short previous-step grace period. Forward-step grace is intentionally not accepted; clients are expected to keep device time synced.
- `ITotpSecretProtector` / `DataProtectionTotpSecretProtector` - protects TOTP secrets at rest.
- `ITwoFactorChallengeIssuer` - issues pending login challenges.
- `ITwoFactorRequirementEvaluator` - policy-ready hook for deciding whether local 2FA is required before token issuance.

Wolverine handler discovery requires constructor-injected types and return types to be public-visible.

---

## Configuration

Sensitive values, especially `JwtOptions.SigningKey`, live in user-secrets or a secret store, not config files. See [`../../../../CONFIG.md`](../../../../CONFIG.md).

- Shared JWT options: `JwtOptions`, bound from `Jwt:`.
- Users options: `UsersOptions`, bound from `Modules:Users:`. Includes access-token lifetime, refresh-token lifetime, session cap, password policy, token lifetimes, two-factor settings, and legal document versions.
- TOTP secrets are protected with ASP.NET Core Data Protection. Production deployments must persist and share the Data Protection key ring across API instances; losing the key ring makes existing TOTP secrets undecryptable.

---

## Integration events

Events are published from `Modulith.Modules.Users.Contracts/Events/`. Consumers live in other modules; Users does not know who subscribes.

Important events include registration, login/logout, password reset/change, email change, erasure request, onboarding completion, two-factor enabled/disabled, and recovery-code regeneration. Raw token events are for Notifications only and must never be logged.

---

## Known footguns

- Never inject `IPasswordHasher`, `IJwtGenerator`, or `IRefreshTokenIssuer` into domain types.
- `JwtOptions.SigningKey` must be at least 32 characters.
- Do not change `User.Email` directly. `ChangeEmail` is the only state transition after creation.
- Do not bypass `SetPassword`; it applies password policy and hashing.
- Do not return specific forgot-password, request-email-change, or invalid-token errors that create account or token-validity oracles.
- Never log raw tokens, TOTP secrets, or recovery codes.
- Compare hashes with `CryptographicOperations.FixedTimeEquals` for application-level equality checks.
- `SweepExpiredTokensHandler` also sweeps pending 2FA challenges. Do not add a separate cleanup.
- Do not reveal the 2FA failed-attempt cap. Return invalid-code errors until the consumed challenge naturally becomes invalid/expired on the next request.
- Do not remove `xmin` from `PendingTwoFactorChallenge`; failed-attempt counting is security-sensitive.

---

## Explicitly out of scope

Do not implement these without an explicit request:

- Email confirmation as an access gate.
- Account lockout after failed logins.
- Breach password checks.
- External identity providers.
- Multi-tenancy or organization membership.
- Admin user impersonation.

Authentication stays in Users. JWT validation is shared infrastructure, but issuance and refresh-token lifecycle stay here.
