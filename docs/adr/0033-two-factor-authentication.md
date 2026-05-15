# ADR-0033: TOTP Two-Factor Authentication

## Status

Accepted

## Context

ADR-0028 originally deferred two-factor authentication because the correct shape depends on product policy, recovery procedures, device trust, and whether the app should prefer TOTP, WebAuthn, or passkeys.

The template now needs a pragmatic 2FA baseline while still leaving policy enforcement open. The important constraint is that 2FA should be optional per user today, but the login flow should not need to be reshaped later when a product decides to require 2FA for selected users, roles, tenants, or sensitive actions.

## Decision

Ship optional per-user TOTP two-factor authentication in the Users module.

The baseline includes:

- TOTP setup and confirmation.
- Recovery codes as hashed, single-use fallback credentials.
- Short-lived pending login challenges after password or linked Google login succeeds.
- A login response envelope with `status`, `session`, and `challenge` shapes so clients branch explicitly on whether 2FA is required.
- A policy-ready `ITwoFactorRequirementEvaluator` hook for future enforcement rules.

TOTP secrets are protected at rest with ASP.NET Core Data Protection. Production deployments must persist and share the Data Protection key ring across API instances; losing the key ring makes existing TOTP secrets undecryptable.

Recovery codes and pending challenge tokens are generated with high entropy and stored only as SHA-256 hashes. Raw recovery codes are returned only when generated. Raw challenge tokens are returned only when issued.

Pending challenges expire, are consumed on success, and are consumed after the failed-attempt cap. Failed-attempt responses intentionally remain opaque around the lockout transition. The challenge row uses Postgres `xmin` optimistic concurrency so parallel failed attempts do not silently undercount.

Enabling and disabling 2FA preserve the current refresh-token session while revoking the user's other active sessions. Regenerating recovery codes requires the current password plus TOTP and does not revoke sessions.

## Rules

- Do not issue access or refresh tokens from password or linked Google login when `ITwoFactorRequirementEvaluator` requires 2FA.
- Do not put user identity in the 2FA challenge response. The challenge token is the client-facing handle.
- Do not store TOTP secrets in plaintext; use `ITotpSecretProtector`.
- Do not store raw recovery codes or challenge tokens.
- Do not reveal whether a failed 2FA attempt hit the attempt cap.
- Do not remove optimistic concurrency from pending 2FA challenges.
- Keep recovery-code input normalization before hashing so uppercase entry works.
- Keep local 2FA enforcement in the Users module; other modules should not query Users tables to make authentication decisions.

## Consequences

**Positive:**

- The template has a complete TOTP baseline without ASP.NET Identity.
- Future required-2FA policy can be added behind the evaluator without changing the API envelope.
- Password and linked Google login share the same local 2FA completion path.
- Recovery codes give users a supported fallback path.

**Negative:**

- Data Protection key-ring persistence becomes part of the authentication runbook.
- TOTP is weaker than WebAuthn/passkeys against phishing and real-time relay attacks.
- Clients must handle two login success modes instead of assuming every 200 response contains a session.
- Account recovery and admin reset policies remain product-specific and are not fully solved by the baseline.
