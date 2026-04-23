---
name: auth-flows
description: Workflow for implementing and modifying authentication flows in the Users module. Covers login, refresh tokens, logout, password reset/change, email change, and their security invariants.
---

# Auth Flows

Use this skill when you are changing authentication behavior in the Users module.

Typical triggers:

- login and token issuance
- refresh-token rotation
- logout or logout-all
- forgot password or password reset
- authenticated password change
- email change request and confirmation

Do not use this skill when:

- the task is only endpoint authorization policy wiring
- the task is changing the RBAC permission model
- the task is adding external identity or 2FA as a new architecture direction without explicit instruction

## Read first

Before changing auth flows, read:

1. `docs/adr/0028-auth-flows-baseline.md`
2. `docs/adr/0029-refresh-tokens-and-logout.md`
3. `docs/how-to/auth/implement-auth-flows.md`
4. one nearby Users feature slice involved in the flow
5. the relevant domain primitives in `Users/Domain/`

## Scope of the shipped baseline

The repo baseline includes:

- register
- login
- refresh token
- logout
- logout all
- get current user
- change password
- request email change
- confirm email change
- forgot password
- reset password

The baseline does not automatically include:

- ASP.NET Identity
- 2FA
- account lockout
- external identity federation
- immediate access-token revocation on every request

If the task moves into those, stop and ask first.

## Core security primitives

Two primitives matter most.

### Refresh tokens

Rules:

- opaque, high-entropy values
- store only the SHA-256 hash server-side
- rotate on refresh
- revoke on logout
- revoke broadly on sensitive account changes

### Single-use tokens

Used for password reset and email change confirmation.

Rules:

- purpose-bound
- hashed at rest
- single-use
- expiration enforced
- never log or persist the raw token outside the delivery path that must send it to the user

## Flow rules

### Login

Login issues:

- short-lived access token
- long-lived refresh token

Do not turn login into a stateful access-token lookup flow. The access token remains stateless.

### Refresh token

Refresh behavior must preserve these invariants:

- incoming refresh token is hashed and looked up
- old token is revoked and linked to the replacement
- new refresh token and new access token are issued together
- rotated-token reuse is treated as suspicious and should force re-authentication

### Logout and logout-all

Logout revokes one refresh token.

Logout-all revokes all refresh tokens for the user.

Do not pretend this instantly invalidates already-issued access tokens. The access-token lifetime bounds that stale-session window.

### Forgot password and reset password

Critical invariants:

- forgot-password must not reveal whether the email exists
- reset-token storage must be hashed
- invalid and expired token responses should not become an oracle
- password reset revokes all refresh tokens

### Change password

Critical invariants:

- require the current password
- revoke all other refresh tokens while preserving the active session when that is the intended behavior
- do not silently let a stolen session rotate the user's password without current-password verification

### Email change

Critical invariants:

- two-step request plus confirmation
- confirmation token is purpose-bound and single-use
- validate uniqueness before committing the change
- revoke refresh tokens after the change

## Anti-enumeration rules

For public recovery flows such as forgot password:

- return consistent response shapes whether the user exists or not
- avoid informative error messages that let callers probe account existence
- keep timing differences bounded and acceptable for the threat model

Do not expose "email not found" from the forgot-password endpoint.

## Token storage rules

Never store bearer tokens in plaintext server-side when they can be hashed safely.

This applies to:

- refresh tokens
- password-reset tokens
- email-change tokens

The raw token may exist only long enough to return to the client or send through the notification channel that delivers it.

## Revocation rules on sensitive events

By default, these flows should trigger refresh-token revocation:

- password reset -> revoke all
- email change -> revoke all
- role change -> revoke all
- password change -> revoke all except the current session when that behavior is intended

Keep the revocation transactional with the sensitive change whenever possible.

## Rate limiting rules

Auth flows are sensitive to abuse.

Keep the appropriate rate-limit policies on auth endpoints. Do not casually remove them while changing the slice.

## Notifications integration rules

Password reset and email change flows may publish integration events that the Notifications module uses to send emails.

Rules:

- publish only the data the downstream notification needs
- keep raw token exposure limited to the event that must carry it to the notification sender
- never log raw tokens

## Common mistakes

Avoid these:

- storing raw refresh or single-use tokens
- returning user-existence signals from recovery flows
- forgetting token-purpose checks for single-use tokens
- forgetting to revoke refresh tokens on sensitive flows
- implementing immediate access-token revocation assumptions in a stateless access-token design
- mixing authentication mechanics with authorization-model changes in the same task without realizing it

## Ask-first cases

Stop and ask before proceeding if:

- the change adds 2FA, passkeys, or external identity federation
- the change introduces server-side access-token revocation checks on every request
- the change redefines token lifetimes or storage in a way that affects the overall security posture

## Definition of done

An auth-flow change is complete when:

- the relevant flow still preserves the repo's token and revocation invariants
- sensitive tokens are hashed at rest
- public recovery flows do not leak account existence
- refresh-token behavior remains rotation-based and revokable
- integration tests cover the changed happy path and core failure path

## Reference material

Use these as the source of truth:

- `docs/adr/0028-auth-flows-baseline.md`
- `docs/adr/0029-refresh-tokens-and-logout.md`
- `docs/how-to/auth/implement-auth-flows.md`
- `docs/how-to/auth/bootstrap-admin.md`
