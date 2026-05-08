# AGENTS.md - Users Module

This module owns the user identity lifecycle: registration, authentication, profile management, RBAC, GDPR user data handling, and Google external login. It does not use ASP.NET Identity; see ADR-0007.

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
- **ExternalLogin** - links a `(Provider, Subject)` pair to a `User`; unique across all users.
- **PendingExternalLogin** - pre-account record for the Google email loop. Token is random, hashed, single-use, and expires.
- **TermsAcceptance** - immutable record of a legal document version accepted by a user.

---

## Shipped flows

- Register, login, refresh token rotation, logout, logout all sessions.
- Get current user, list users, get user by ID, change user role.
- Forgot password, reset password, change password.
- Request and confirm email change.
- Consent tracking, GDPR export, GDPR erasure.
- Google login email loop, confirm, link, unlink, set initial password, complete onboarding.

---

## Invariants

1. Email addresses are unique across the `users` schema and stored lowercased.
2. Passwords are BCrypt-hashed before the aggregate ever sees them. Plaintext passwords never enter the domain.
3. Token values (`SingleUseToken`, `RefreshToken`, pending external login token) are stored as SHA-256 hashes. Raw values exist only in HTTP responses to Notifications and in email bodies.
4. Enumeration is not possible through forgot-password, request-email-change, or Google login. Keep responses uniform.
5. Security-sensitive events revoke refresh tokens: password reset revokes all; password change revokes all except the current session; email change confirmation revokes all.
6. Refresh-token reuse triggers full chain revocation and forced re-login.
7. External-only users have `PasswordHash = null` and `HasCompletedOnboarding = false` until dedicated slices fill them.
8. `ExternalLogin` is unique per `(Provider, Subject)` globally and per `(User, Provider)` for each user.
9. A password-less user cannot unlink their only external login.
10. `CompleteOnboarding` is idempotent for the current terms-of-service version.
11. Use `IClock`; never use `DateTime.UtcNow` in this module.

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
- `IGoogleIdTokenVerifier` / `GoogleIdTokenVerifier` - verifies Google ID tokens against JWKS cached in `IMemoryCache`. Returns `ExternalAuthUnavailable` if JWKS fetch fails.

Wolverine handler discovery requires constructor-injected types and return types to be public-visible. Keep `IGoogleIdTokenVerifier` and `GoogleIdentity` public.

---

## Configuration

Sensitive values, especially `JwtOptions.SigningKey`, live in user-secrets or a secret store, not config files. See [`../../../../CONFIG.md`](../../../../CONFIG.md).

- Shared JWT options: `JwtOptions`, bound from `Jwt:`.
- Google options: `GoogleAuthOptions`, bound from `Modules:Users:Google:`. `ClientId` is required. `JwksUri` defaults to Google's OAuth cert endpoint. `JwksCacheDuration` defaults to 60 minutes.
- Users options: `UsersOptions`, bound from `Modules:Users:`. Includes access-token lifetime, refresh-token lifetime, session cap, password policy, token lifetimes, pending external-login lifetime, and legal document versions.

---

## Integration events

Events are published from `Modulith.Modules.Users.Contracts/Events/`. Consumers live in other modules; Users does not know who subscribes.

Important events include registration, login/logout, password reset/change, email change, erasure request, external login pending/linked/unlinked, external provisioning, and onboarding completion. Raw token events are for Notifications only and must never be logged.

---

## Google login concurrency rules

Do not modify `GoogleLoginHandler` fast path or `UnlinkGoogleLoginHandler` without preserving the `FOR UPDATE` lock and publish-before-commit order:

1. `FOR UPDATE` lock.
2. State mutation.
3. `SaveChangesAsync`.
4. `bus.PublishAsync`.
5. Wolverine commits.

Do not add explicit EF transactions inside these handlers; Wolverine transaction middleware already owns the transaction.

Do not add `ExpiresAt > now` back to the Step 1 reuse check in `GoogleLoginHandler`. Expired but unconsumed rows still block the partial unique index and must be refreshed rather than skipped.

Do not widen `LinkExternalLogin` duplicate checks back to `(provider, subject)`. The guard is intentionally provider-level so a user can have at most one Google account.

---

## Known footguns

- Never inject `IPasswordHasher`, `IJwtGenerator`, or `IRefreshTokenIssuer` into domain types.
- `JwtOptions.SigningKey` must be at least 32 characters.
- Do not change `User.Email` directly. `ChangeEmail` is the only state transition after creation.
- Do not bypass `SetPassword`; it applies password policy and hashing.
- Do not return specific forgot-password, request-email-change, or invalid-token errors that create account or token-validity oracles.
- Never log raw tokens.
- Compare hashes with `CryptographicOperations.FixedTimeEquals` for application-level equality checks.
- `SweepExpiredTokensHandler` also sweeps pending external logins. Do not add a separate cleanup.

---

## Explicitly out of scope

Do not implement these without an explicit request:

- Two-factor authentication.
- Email confirmation as an access gate.
- Account lockout after failed logins.
- Breach password checks.
- Additional external identity providers beyond Google.
- Multi-tenancy or organization membership.
- Admin user impersonation.

Authentication stays in Users. JWT validation is shared infrastructure, but issuance and refresh-token lifecycle stay here.
