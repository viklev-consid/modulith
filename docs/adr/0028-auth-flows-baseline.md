# ADR-0028: Baseline Authentication Flows in the Users Module

## Status

Accepted

## Context

ADR-0007 established that the Users module is a lightweight custom aggregate rather than ASP.NET Identity. That ADR intentionally kept the initial scope small — register, login, get-current-user — and deferred most authentication-adjacent flows as "extension points."

That scope proved too lean for a general-purpose template. Nearly every real application needs:

- Password reset (forgotten password recovery)
- Changing password while authenticated
- Changing email address (with confirmation)
- Logout

Leaving these out forces every team using the template to build them, inconsistently, usually late, and usually with subtle security mistakes. The cost of shipping correct reference implementations is modest compared to the cost of every downstream team rediscovering the same footguns.

Two-factor authentication, email confirmation gating, account lockout, and federated identity remain deferred — those are genuinely product decisions with strong context-dependent trade-offs. See the explicit extension-points section below.

## Decision

Expand the Users module's default feature set to include:

1. **Forgot password / password reset** — tokenized email-based reset.
2. **Change password (authenticated)** — requires current password; separate from reset.
3. **Change email (with confirmation)** — tokenized two-step flow.
4. **Logout / logout-everywhere** — driven by refresh token revocation (ADR-0029).

### Token model for password reset and email change

A single generalized `SingleUseToken` value object handles both flows:

- **Token value**: cryptographically random, 256-bit minimum, encoded as URL-safe base64.
- **Storage**: *hashed* with SHA-256 (tokens are bearer credentials; unhashed storage means a DB dump equals account takeover).
- **Lifetime**: 30 minutes default, configurable per purpose via `UsersOptions`.
- **Purpose**: enum discriminator (`PasswordReset`, `EmailChange`) to prevent cross-purpose reuse.
- **Single-use**: `ConsumedAt` timestamp; verification fails if set.
- **Bound to user**: `UserId` ties the token to its owner.

Stored in a `user_tokens` table in the `users` schema. Expired or consumed tokens are swept by a scheduled Wolverine job (implements `IRetainable`, per ADR-0012).

### Password reset flow

- `POST /v1/users/password/forgot` — public, heavily rate-limited (`auth` policy), always returns 200 regardless of whether the email exists.
- If the email matches a user: generate a token, store its hash, publish `PasswordResetRequested`. Notifications module sends the reset email with the raw token.
- `POST /v1/users/password/reset` — public, rate-limited (`auth`), takes `{ token, newPassword }`. Verifies token (hashes + compares + checks not consumed + checks not expired + checks purpose), resets password, consumes token, revokes all refresh tokens (security-sensitive event), publishes `PasswordReset`.

### Change password (authenticated)

- `POST /v1/users/me/password` — authenticated, rate-limited (`auth`), takes `{ currentPassword, newPassword }`.
- Validates current password, updates hash, revokes all refresh tokens except the one on the current request, publishes `PasswordChanged`.

### Change email (with confirmation)

- `POST /v1/users/me/email/request` — authenticated, rate-limited (`write`), takes `{ newEmail, currentPassword }`.
- Validates current password, validates email uniqueness, generates token, stores hash, publishes `EmailChangeRequested`. Notifications sends confirmation to the *new* address.
- `POST /v1/users/me/email/confirm` — authenticated, rate-limited (`auth`), takes `{ token }`.
- Verifies token, updates email, consumes token, publishes `EmailChanged`.
- Notification fired to the *old* email address too, alerting the user of the change (defense-in-depth against silent account takeover).

### Logout

Covered by ADR-0029 (Refresh Tokens). `POST /v1/users/logout` revokes the refresh token used in the request. `POST /v1/users/logout/all` revokes every refresh token belonging to the user.

### Security-sensitive events revoke refresh tokens

By convention, these operations revoke all the user's refresh tokens (or all except the current one, where appropriate):

- Password reset (all)
- Password change (all except current — user's active session continues)
- Email change (all — treat as a full session invalidation)
- External login unlink (all — if the provider account was compromised, all sessions issued via it are closed)

### Anti-enumeration

`POST /v1/users/password/forgot` always returns 200 with a generic body. No timing differences between "email exists" and "email doesn't exist" — use `CryptographicOperations.FixedTimeEquals` where applicable and ensure branches have comparable cost.

`POST /v1/users/register` likewise returns a consistent response shape whether the email is taken or not, surfacing the "already taken" error only where legitimately needed (the register slice itself, where the client knows they just tried to register).

## Extension points — explicitly deferred

These are **not** in the template. Teams add them when their threat model demands it.

### Two-factor authentication

TOTP, WebAuthn/passkeys, SMS, backup codes. Deferred because:

- 2FA correctness is subtle (recovery flows, device trust, admin reset procedures) and half-implementing is worse than not having it.
- Modern best practice is shifting to WebAuthn/passkeys; shipping TOTP-based 2FA bakes in an older pattern.
- Real requirements vary enormously.

Extension shape: add a `TwoFactorMethod` entity (own table, one-to-many with User), new slices under `Features/TwoFactor/`, integrate with Login slice to issue a partial "challenge" token when 2FA is enabled. Document this pattern in `Users/CLAUDE.md`.

### Email confirmation as a gate on access

Some apps require confirmed email before any authenticated action. Others allow unconfirmed users to proceed with limited access. Some don't require confirmation at all. This is a product decision.

Extension shape: a `EmailConfirmedAt` on User, a `RequireConfirmedEmail` authorization policy, a confirmation slice that reuses the `SingleUseToken` primitive. The infrastructure is there; the policy is not.

### Account lockout

Lockout after N failed login attempts. Deferred because:

- Rate limiting on `auth` policy (already in template) handles the common credential-stuffing case without the footguns.
- Lockout policies vary (lock the account? the IP? both? exponential backoff? alert on lockout?).
- Lockout itself is a denial-of-service vector.

Extension shape: a `FailedLoginTracker` aggregate (keyed by email + IP), slice middleware on Login.

### Breach password check

Integration with HaveIBeenPwned's k-anonymity API to reject known-compromised passwords. Not baked in because:

- External dependency + rate limiting.
- Privacy consideration even with k-anonymity.
- Better done at the password-policy layer (a pluggable `IPasswordPolicy`) which the template also doesn't prescribe.

### External identity providers (OIDC federation)

Auth0, Entra ID, Keycloak, Cognito, Google, etc. Deferred because the lightweight custom user aggregate is the template's opinionated choice. Federation is a replacement, not an addition.

Extension shape: replace the Register/Login slices with an OIDC callback flow that projects the external identity into the local User aggregate. Keep the User aggregate as the local source of truth.

### Multi-tenancy / organization membership

Significant enough to be its own module, not just a feature extension. Deferred entirely.

## Consequences

**Positive:**

- Users module is production-ready for typical apps out of the box.
- Consistent implementation across apps using the template — no reinvention of the same flows.
- Security-sensitive patterns (anti-enumeration, token hashing, revocation on privilege changes) are modeled correctly from day one.
- Refresh-token revocation on sensitive events (ADR-0029 integration) means password changes actually kick the attacker out.

**Negative:**

- More code in the Users module. Accepted — it's business-relevant, not architectural bloat.
- Additional configuration surface (token lifetimes, rate-limit windows). Mitigated by sensible defaults in `UsersOptions`.
- The extension-points list is long. Some teams will expect the "missing" ones to be in-scope and be surprised. Documented explicitly.
- `SingleUseToken` is a generalized primitive — needs discipline to keep clean. Don't accumulate per-purpose token types; extend the `Purpose` enum.

## Related

- ADR-0007 (No ASP.NET Identity): establishes the lightweight approach these flows build on.
- ADR-0012 (GDPR Primitives): tokens are `IRetainable`, swept by the retention job.
- ADR-0014 (Notifications): reset/confirmation emails go through the Notifications module.
- ADR-0018 (Rate Limiting): auth policy applied to all these endpoints.
- ADR-0029 (Refresh Tokens): logout mechanics and session revocation.
