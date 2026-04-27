# CLAUDE.md — Users Module

This module owns the user identity lifecycle: registration, authentication, and profile management. It does **not** use ASP.NET Identity (see ADR-0007).

For general module conventions, see [`../CLAUDE.md`](../CLAUDE.md).

---

## Domain vocabulary

- **User** — the root aggregate. Identified by `UserId` (typed Guid).
- **Email** — a value object. Always stored lowercased. Validated on creation via `Email.Create(...)`.
- **PasswordHash** — a value object wrapping a BCrypt hash. Never log or serialize the raw value (`ToString()` returns `[REDACTED]`).
- **DisplayName** — a plain string, 1-100 characters.
- **SingleUseToken** — tokenized primitive for password reset and email change. Stored as SHA-256 hash; raw value exists only in transit.
- **RefreshToken** — entity for session management. Stored hashed, rotated on each use.
- **PendingEmailChange** — holds the requested-but-unconfirmed new email alongside a `SingleUseToken` hash until confirmation completes.
- **Consent** — entity tracking user agreement to processing purposes (GDPR).
- **ExternalLogin** — entity linking a `(Provider, Subject)` pair to a `User`. Owned by the `User` aggregate via `_externalLogins`. Unique constraint on `(provider, subject)` across all users.
- **ExternalLoginProvider** — enum (`Google`).
- **PendingExternalLogin** — pre-account record created during the email-loop for unlinked Google accounts. Identified by a random 256-bit token, stored as SHA-256 hash. Not linked to a `User` (may precede account creation). Single-use via `Consume(IClock)`; expires after `PendingExternalLoginLifetime`.
- **TermsAcceptance** — immutable record that a user accepted a specific version of a legal document (e.g. `"tos:1.0"`). `CompleteOnboarding` writes a single ToS row keyed to the current `UsersOptions.TermsOfServiceVersion`. Keyed by `(UserId, Version)`, unique constraint.

---

## What this module does

**Baseline flows:**

- Register / Login / GetCurrentUser
- Forgot password / Reset password
- Change password (authenticated)
- Request email change / Confirm email change
- Refresh token rotation
- Logout (single session) / Logout all sessions
- Consent tracking
- User data export (GDPR)
- User erasure (GDPR)

**Third-party authentication — Google (shipped — Phase 14):**

- Uniform email-loop: `POST /v1/users/auth/google/login` verifies the Google ID token via `IGoogleIdTokenVerifier`. If `(Google, subject)` is already linked → fast path, tokens issued immediately (200). Otherwise → create `PendingExternalLogin`, publish `ExternalLoginPendingV1`, return 202. This applies to both new and existing email addresses — the caller cannot distinguish the two cases.
- `POST /v1/users/auth/google/confirm` consumes the raw token from the email, provisions or links the account, issues tokens.
- `POST /v1/users/me/auth/google/link` — links Google to an existing authenticated user.
- `DELETE /v1/users/me/auth/google/unlink` — unlinks Google; enforces credential-retention guardrail.
- `POST /v1/users/me/password/initial` — sets the first password for external-only users.
- `POST /v1/users/me/onboarding` — requires `acceptTerms: true`; optional `acceptMarketingEmails: bool`. Records ToS acceptance, optionally grants `marketing-emails` consent, marks `HasCompletedOnboarding = true`.
- `IGoogleIdTokenVerifier` / `GoogleIdTokenVerifier` — verifies Google ID tokens via JWKS (cached in `IMemoryCache`). Fail-closed: returns `ExternalAuthUnavailable` if JWKS cannot be fetched.
- `GoogleAuthOptions` — bound from `Modules:Users:Google:`. `ClientId` is `[Required]`. `JwksUri` defaults to `https://www.googleapis.com/oauth2/v3/certs`. `JwksCacheDuration` defaults to 60 minutes.

**RBAC (shipped — Phase 13):**

- `Role` value object on `User` aggregate (`admin` / `user`)
- Permission constants in `Users.Contracts/Authorization/UsersPermissions.cs`
- `PermissionCatalog` discovers all `*Permissions` types from `*.Contracts` assemblies at startup
- `PermissionClaimsTransformation` adds `"permission"` claims per request from JWT role
- Per-permission `AuthorizationPolicy` instances registered at startup
- `PUT /v1/users/{userId}/role` — requires `users.roles.write` (admin only)
- `GET /v1/users` — requires `users.users.read` (admin only)
- `GET /v1/users/{userId}` — requires `users.users.read` (admin only)
- `GET /v1/users/me` — returns `userId` (Guid), `email`, `displayName`, `createdAt`, `role`, `permissions[]`, `permissionsVersion`, `hasPassword` (bool), `hasCompletedOnboarding` (bool), `linkedProviders` (sorted string[])
- `AdminBootstrapper` hosted service for non-dev environments
- See `docs/how-to/auth/use-rbac.md` and ADR-0030

---

## Invariants

1. Email addresses are unique across the `users` schema. Stored lowercased.
2. Passwords are BCrypt-hashed before the aggregate ever sees them. Plaintext passwords never enter the domain.
3. `PasswordHash.ToString()` returns `[REDACTED]` — the raw hash is never rendered in logs or debugger output.
4. `User.CreateWithPassword(...)` raises `UserRegistered` domain event; `User.CreateExternal(...)` raises `UserProvisionedFromExternal`. Publisher handlers map these to integration events via the Wolverine outbox.
13. External-only users (`CreateExternal`) have `PasswordHash = null` and `HasCompletedOnboarding = false`. Both are filled in via dedicated slices.
14. `ExternalLogin` is unique per `(Provider, Subject)` globally — one Google account can only be linked to one user.
15. Credential-retention guardrail: `UnlinkExternalLogin` fails if the user has no other credential (password or other external login).
16. Uniform email-loop: `GoogleLogin` never reveals whether an email is registered. Both new and existing emails get 202.
17. `PendingExternalLogin` tokens are stored as SHA-256 hashes. Raw value exists only in the email body. `IsValid` returns false if expired OR already consumed.
18. `TermsAcceptance` inserts are idempotent in the handler — re-submitting `CompleteOnboarding` when a ToS row for the current version already exists is a no-op on the DB level.
5. `SingleUseToken` values are stored as SHA-256 hashes. Raw tokens exist only in HTTP responses (to Notifications) and in email bodies. Never logged, never persisted in plaintext.
6. `RefreshToken` values are stored as SHA-256 hashes with the same discipline.
7. Password comparison uses constant-time equality (BCrypt's default).
8. Enumeration is not possible via `POST /v1/users/password/forgot` or `POST /v1/users/me/email/request` responses — they return identical success shapes regardless of whether the email is known/available.
9. Security-sensitive events revoke refresh tokens:
   - **Password reset** → revoke all.
   - **Password change** → revoke all except the one on the current request.
   - **Email change confirmation** → revoke all.
10. Refresh-token reuse (presenting an already-rotated token) triggers full chain revocation and forced re-login.
11. Email change alerts are sent to the **old** email address after completion, defense-in-depth against silent account takeover.
12. Rate limits: `auth` policy on all credential-handling endpoints; `write` policy on session-ending endpoints (see the slice table below).

---

## Auth concerns

- **JWT validation config lives in shared `JwtOptions`** (in `Shared.Infrastructure.Auth`), bound from the `Jwt:` config section by `Program.cs`. Shared across modules so tokens issued here are accepted wherever validation runs.
- **Access tokens** are signed with a symmetric key from `JwtOptions.SigningKey` (HMAC-SHA256, minimum 32 characters, enforced by `ValidateDataAnnotations()` + `ValidateOnStart()`). Issued by `JwtGenerator` inside this module.
- **Refresh tokens** are opaque 256-bit random values, persisted hashed. Lifetime and session-cap settings live in `UsersOptions` (refresh tokens are a Users-module concern, not shared infrastructure).
- **`ICurrentUser`** reads from `HttpContext.User` claims. `ClaimTypes.NameIdentifier` holds the `UserId` (Guid string). An additional claim carries the active refresh token ID so `ChangePassword` can preserve the current session while revoking others.
- **`GetCurrentUser`** parses the `NameIdentifier` claim and queries by `UserId`. Missing or unparseable claim → 401.

---

## Slice inventory and rate limits

| Slice | Endpoint | Auth | Rate limit |
|---|---|---|---|
| Register | `POST /v1/users/register` | public | `auth` |
| Login | `POST /v1/users/login` | public | `auth` |
| GetCurrentUser | `GET /v1/users/me` | authenticated | `read` |
| ForgotPassword | `POST /v1/users/password/forgot` | public | `auth` |
| ResetPassword | `POST /v1/users/password/reset` | public | `auth` |
| ChangePassword | `POST /v1/users/me/password` | authenticated | `auth` |
| RequestEmailChange | `POST /v1/users/me/email/request` | authenticated | `write` |
| ConfirmEmailChange | `POST /v1/users/me/email/confirm` | authenticated | `auth` |
| RefreshToken | `POST /v1/users/token/refresh` | refresh-token-authed | `auth` |
| Logout | `POST /v1/users/logout` | authenticated | `write` |
| LogoutAll | `POST /v1/users/logout/all` | authenticated | `write` |
| ChangeUserRole | `PUT /v1/users/{userId}/role` | `users.roles.write` | `write` |
| ListUsers | `GET /v1/users` | `users.users.read` | `read` |
| GetUserById | `GET /v1/users/{userId}` | `users.users.read` | `read` |
| GoogleLogin | `POST /v1/users/auth/google/login` | public | `auth` |
| GoogleLoginConfirm | `POST /v1/users/auth/google/confirm` | public | `auth` |
| LinkGoogleLogin | `POST /v1/users/me/auth/google/link` | authenticated | `write` |
| UnlinkGoogleLogin | `DELETE /v1/users/me/auth/google/unlink` | authenticated | `write` |
| SetInitialPassword | `POST /v1/users/me/password/initial` | authenticated | `auth` |
| CompleteOnboarding | `POST /v1/users/me/onboarding` | authenticated | `write` |

---

## Security abstractions (module-internal)

- `IPasswordHasher` / `BcryptPasswordHasher` — hashing is infrastructure, called from handlers. Never inject into domain types.
- `IJwtGenerator` / `JwtGenerator` — issues access tokens. Depends on shared `JwtOptions` from `Shared.Infrastructure.Auth`. Same options drive token validation in the API pipeline, so tokens issued here are accepted there by construction.
- `IRefreshTokenIssuer` — issues and rotates `RefreshToken` entities. Handles device fingerprinting (UA + IP, opaque) for session context.
- `ISingleUseTokenService` — creates and verifies `SingleUseToken` instances for password reset and email change.
- `IGoogleIdTokenVerifier` / `GoogleIdTokenVerifier` — verifies Google ID tokens against Google's JWKS endpoint. JWKS is cached in `IMemoryCache` (`GoogleAuthOptions.JwksCacheDuration`, default 60 min). Returns `ExternalAuthUnavailable` if JWKS fetch fails (fail-closed). **Must be `public` interface** — Wolverine handler discovery requires all injected types to be public.

---

## Configuration

JWT concerns → shared `JwtOptions` (in `Shared.Infrastructure.Auth`), bound from `Jwt:` config section.

Google options → `GoogleAuthOptions`, bound from `Modules:Users:Google:`. `ClientId` is `[Required]` — must be set in all environments including tests (tests use `"test-google-client-id"` via `ApiTestFixture`). `JwksUri` defaults to `https://www.googleapis.com/oauth2/v3/certs`. `JwksCacheDuration` defaults to 60 minutes.

Users-specific options → `UsersOptions`, bound from `Modules:Users:`:

```csharp
public sealed class UsersOptions
{
    [Range(1, 1440)]
    public int AccessTokenLifetimeMinutes { get; init; } = 15;

    [Range(1, 365)]
    public int RefreshTokenLifetimeDays { get; init; } = 30;

    [Range(1, 100)]
    public int MaxActiveRefreshTokensPerUser { get; init; } = 10;

    public TimeSpan PasswordResetTokenLifetime { get; init; } = TimeSpan.FromMinutes(30);
    public TimeSpan EmailChangeTokenLifetime { get; init; } = TimeSpan.FromMinutes(30);

    [Range(8, 128)]
    public int MinPasswordLength { get; init; } = 10;

    public TimeSpan PendingExternalLoginLifetime { get; init; } = TimeSpan.FromMinutes(15);
    [Range(1, 20)]
    public int MaxPendingExternalLoginsPerEmail { get; init; } = 5;
    [Required]
    public string TermsOfServiceVersion { get; init; } = "1.0";
    [Required]
    public string PrivacyPolicyVersion { get; init; } = "1.0";
}
```

Sensitive values (e.g., `JwtOptions.SigningKey`) live in user-secrets / secret store, not config files. See [`../../../CONFIG.md`](../../../CONFIG.md).

---

## Integration events published

In `Modulith.Modules.Users.Contracts/Events/`:

- `UserRegisteredV1`
- `UserLoggedInV1`
- `UserLoggedOutAllDevicesV1`
- `PasswordResetRequestedV1` — carries raw reset token for Notifications only
- `PasswordResetV1`
- `PasswordChangedV1`
- `EmailChangeRequestedV1` — carries raw token for Notifications only
- `EmailChangedV1` — carries old + new email so Notifications alerts both
- `UserDeactivatedV1`
- `UserErasureRequestedV1` — triggers cross-module erasure per ADR-0012
- `ExternalLoginPendingV1` — carries raw confirmation token for Notifications; signals whether recipient is a new or existing user
- `ExternalLoginLinkedV1` — carries `Email` (for Notifications cross-module use), `Provider`, `Subject`, `LinkedAt`
- `ExternalLoginUnlinkedV1` — carries `Email`, `Provider`
- `UserProvisionedFromExternalV1` — raised when a new user is created via Google confirm
- `UserOnboardingCompletedV1` — raised when `CompleteOnboarding` is accepted

Handlers for these events live in consuming modules (Notifications, Audit, any others). Users does not know who subscribes.

---

## Known footguns

- **Never inject `IPasswordHasher` into domain types.** Hashing is infrastructure, called from handlers. Domain stays pure.
- **Never inject `IJwtGenerator` or `IRefreshTokenIssuer` into domain types.** Same reason.
- **`JwtOptions.SigningKey` must be ≥ 32 characters.** HMAC-SHA256 requires a minimum key length. `ValidateDataAnnotations()` + `ValidateOnStart()` enforce this at startup.
- **EF value converters for `Email` and `PasswordHash`** — if LINQ queries against these properties behave unexpectedly, check the converter configuration in `UserConfiguration`.
- **Don't change `User.Email` directly.** `ChangeEmail` is the only state transition. During `Create`, the factory method handles initial assignment.
- **Don't bypass `SetPassword`.** It applies the password policy and hashes. Direct assignment bypasses both.
- **Don't forget to revoke refresh tokens on sensitive events.** See the invariants table above. Missing a revocation means an attacker's session survives a password reset.
- **Role changes have a stale-permission window equal to the access token lifetime (default 15 min).** Revoking refresh tokens prevents new tokens from carrying the old role, but any live access token still authorizes with its embedded role claim until it expires. `GET /v1/users/me` mirrors the token's role claim (not the DB), so the response stays consistent with what the token actually authorizes. This is the accepted stateless-JWT tradeoff. See ADR-0030 if you need immediate revocation.
- **Don't return specific errors from `ForgotPassword`.** Always the same response shape, always 200.
- **Don't return specific errors from `RequestEmailChange` when the target email is taken.** Same response as success.
- **Don't distinguish "invalid token" from "expired token"** in reset/confirm error responses. Use a single `InvalidOrExpiredToken` error — otherwise it's an oracle for token validity.
- **Never log raw tokens.** Serilog destructuring masks properties matching token-related patterns, but don't rely on that — never include a raw token in a log call in the first place.
- **Never use `DateTime.UtcNow`.** Use `IClock`. Tests need control over time, and security invariants (token expiry) need deterministic verification.
- **Compare hashes with `CryptographicOperations.FixedTimeEquals`** when doing application-level equality checks on token hashes or password hashes. (DB-level lookups via B-tree seek are fine; the timing attack surface is application-code comparison.)
- **`MaxActiveRefreshTokensPerUser` enforcement.** When at the limit, the oldest active token is revoked on login to make room. Unbounded session counts would grow the `refresh_tokens` table without limit.
- **`IGoogleIdTokenVerifier` must be `public`.** Wolverine handler discovery requires all constructor-injected types to be public-visible. `GoogleIdentity` (the return type) must also be `public` for the same reason.
- **Don't put PII in `PendingExternalLogin.Email`.** The email is needed to dispatch the notification but is not personal data in the same sense as a user profile — it's a transient pre-account record keyed by the Google identity, not the user's stored email. The sweep job removes it after expiry.
- **`SweepExpiredTokensHandler` also sweeps `PendingExternalLogins`.** Expired AND consumed records are deleted in the same job. Don't add a separate cleanup.
- **`ExternalLoginLinkedV1` and `ExternalLoginUnlinkedV1` carry `Email`.** This is required for Notifications to send alerts without crossing module boundaries. If you add more external-login events, add `Email` if Notifications needs it.
- **Don't modify `GoogleLoginHandler` (fast path) or `UnlinkGoogleLoginHandler` without preserving the `FOR UPDATE` lock.** Both handlers hold a `SELECT … FOR UPDATE` on the same `external_logins` row — login by `(provider, subject)`, unlink by `(user_id, provider)`. Login wraps the lock and token issuance in an explicit transaction (`BeginTransactionAsync` → `FOR UPDATE` → `IssueAsync` → `CommitAsync`). Unlink wraps the lock, delete, and revocation in an equivalent transaction. Removing either lock, collapsing them into separate transactions, or re-ordering the lock and the state read re-opens the race where login can mint a valid session after unlink completed.

---

## Explicitly out of scope (extension points)

The following are deliberately not shipped. Each is documented here with the shape a team would use to add it. **Do not implement these without an explicit request.**

### Two-factor authentication

**Not shipped because:** 2FA correctness is subtle (recovery flows, device trust, admin reset procedures). Modern best practice is shifting toward WebAuthn/passkeys, and shipping TOTP bakes in an older pattern. Real requirements vary.

**Extension shape:**

- Add `TwoFactorMethod` entity (one-to-many with User): `Method` (TOTP/WebAuthn), `SecretOrCredential`, `EnabledAt`, `LastUsedAt`.
- Add `BackupCode` entity: hashed, single-use, bound to user.
- Slices: `EnrollTwoFactor`, `VerifyEnrollment`, `DisableTwoFactor`, `GenerateBackupCodes`, `VerifyTwoFactor`.
- Modify Login slice: if user has active 2FA, issue a short-lived *challenge* token instead of an access token. Client completes with `VerifyTwoFactor` to exchange challenge + code for access + refresh.
- Add `RequireTwoFactor` authorization policy for sensitive endpoints.
- Libraries: `Otp.NET` for TOTP, `Fido2NetLib` for WebAuthn.

### Email confirmation as an access gate

**Not shipped because:** whether unconfirmed users can act, and what they can do, is product-specific.

**Extension shape:**

- Add `EmailConfirmedAt : DateTimeOffset?` to `User`.
- Reuse `SingleUseToken` with a new `TokenPurpose.EmailConfirmation`.
- Slice: `ConfirmEmail`.
- Add `RequireConfirmedEmail` authorization policy; apply to endpoints that gate on confirmation.
- Emit `EmailConfirmationRequestedV1` from Register (Notifications handles the email).

### Account lockout after failed logins

**Not shipped because:** rate limiting on `auth` policy handles the common credential-stuffing case. Lockout policies vary, and lockout itself is a DoS vector.

**Extension shape:**

- `FailedLoginAttempt` entity keyed by `(Email, Ip)` with a sliding-window count.
- Slice middleware on Login that checks the count and short-circuits before password verification when over threshold.
- Emit `AccountLockedOutV1` for audit and optional user alert.
- Consider separately tracking per-email and per-IP to balance legitimate users behind shared NAT against targeted attacks.

### Breach password check

**Not shipped because:** external dependency + rate-limit concerns. Better done as a pluggable `IPasswordPolicy`.

**Extension shape:**

- `IPasswordPolicy` interface: `Task<PolicyResult> ValidateAsync(string password)`.
- Default implementation: length + complexity rules from `UsersOptions`.
- HIBP implementation (optional): k-anonymity lookup against `api.pwnedpasswords.com`.
- Invoked from `User.SetPassword` via injected policy service.

### External identity providers — Google (shipped — Phase 14)

See "Third-party authentication — Google" under "What this module does" above. Additional providers (GitHub, Microsoft, Apple) can be added by:

1. Adding a value to `ExternalLoginProvider` enum.
2. Adding `IXxxIdTokenVerifier` (same shape as `IGoogleIdTokenVerifier`).
3. Reusing `PendingExternalLogin` and `ExternalLogin` — they are provider-agnostic.
4. Adding six new slices (Login, Confirm, Link, Unlink, SetInitialPassword already exists, CompleteOnboarding already exists).

### Multi-tenancy / organization membership

**Not shipped.** This is its own module, not a Users extension. If added:

- New `Organizations` module with its own schema.
- `OrganizationMembership` as a join entity in Organizations (not Users) referencing `UserId`.
- Authorization policies like `RequireOrganizationMember(role)`.

### Admin user impersonation

**Not shipped** for security-posture reasons. Teams that need it should design it deliberately with strong auditing.

**Extension shape:**

- Admin role + policy.
- Slice: `ImpersonateUser` issues a special access token with `imp` claim identifying the real admin.
- Every request with `imp` claim is audit-logged separately.
- Impersonation is explicitly time-limited, non-refreshable, and cannot perform certain operations (change password, delete account).

---

## Why no separate `Authentication` module

Authentication is a Users-module concern. Splitting it would mean:

- The `User` aggregate loses control of password state.
- Cross-module chatter for every login.
- Artificial boundary with no clear ownership of refresh-token storage.

Instead: authentication lives with the user data it authenticates. The `IJwtGenerator` and `IRefreshTokenIssuer` abstractions allow swapping implementations without cross-module concerns. JWT *validation* settings are shared across modules via `Shared.Infrastructure.Auth` — that's appropriate because multiple modules may need to validate tokens — but *issuance* and *refresh-token lifecycle* stay here.
