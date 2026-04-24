# 0031. Third-party authentication

Date: 2026-04-24
Status: Proposed

## Context

The Modulith baseline supports only email-and-password authentication on the
 custom `User` aggregate (ADR-0007, ADR-0028). We want to let users sign in
with a third-party identity provider — Google first, with Apple / Microsoft /
 GitHub as expected follow-ons — and to let existing users link a Google
 identity to their account.

Several design questions had to be settled before implementation could begin:

1. **Identity model.** Provider columns on `User` (one provider per user)
   vs. a separate `ExternalLogin` entity (multiple providers, mixed
   password + external).
2. **Flow shape.** Backend-driven OAuth (`/challenge` + `/callback`,
   state cookie, PKCE) vs. client-driven (frontend uses the provider's
   native SDK, sends us a verified `id_token`).
3. **First-login policy.** Auto-provision on unknown email, require
   prior registration, or route every first-time flow through an email
   confirmation.
4. **Collision policy.** Auto-link, block, prompt for password, or
   email-confirmation loop.
5. **Enumeration resistance.** Any visible difference in API response
   between "email exists" and "email does not exist" leaks account
   existence. ADR-0028's silent-success forgot-password is the baseline
   precedent.
6. **Password-optional users.** `User.PasswordHash` is currently
   non-nullable. External-only users have no password at provision.
7. **Onboarding and lawful-basis artefacts.** Auto-provisioning skips the
   registration-form ToS gate that password flows implicitly use. We need
   a post-provision step, a way to know whether the user has completed it,
   and a per-user artefact proving acceptance.
8. **Data provenance.** Audit and support tooling must answer "how was
   this user created?" without inferring from event sequences.
9. **Session safety on unlink.** Refresh tokens issued via a provider
   survive up to 30 days after the provider account is compromised unless
   we revoke them.
10. **Abuse containment.** Every first-time Google submission triggers
    outbound email — per-request rate limits and per-record forensic
    data are both required.
11. **GDPR data minimization.** Pending records hold email + display name
    before any account exists. Retention, erasure, and export need
    explicit treatment.
12. **Separation of legal artefacts.** GDPR distinguishes between consent
    (Art. 6(1)(a), narrow and revocable: marketing, analytics) and
    contract agreement (Art. 6(1)(b), e.g. accepting ToS). The existing
    `Consents` registry (ADR-0012) is modeled around Art. 6(1)(a) opt-in
    consent; using it for ToS would conflate two different legal bases.


ADR-0007 explicitly rejected ASP.NET Identity; that rejection still holds.
 Federation does not change the calculus — the surface we need (id_token
 verification, a link table, a pending-confirmation table, a ToS artefact)
 is small enough that adopting ASP.NET Identity for it would be
 net-negative.

## Decision

### Identity model

Introduce an `ExternalLogin` entity owned by `User` with fields
`(UserId, Provider, Subject, LinkedAt)`, unique on `(Provider, Subject)`.
 Supports multiple providers per user and mixed password + external
 credentials.

Make `User.PasswordHash` nullable. External-only users exist without a
 password until they opt in via `SetInitialPassword`. `User.Create` splits
 into `CreateWithPassword` and `CreateExternal`.

Credential-retention guardrail on unlink: a user must always retain at
 least one credential (password or another external login). Enforced on
 the `User` aggregate.

### Flow shape

Client-driven. Frontends use Google Identity Services and POST a verified
`id_token` to the API. No redirect endpoints, no state cookies, no
 platform-specific callback URLs on our side. Security boundary is backend
 JWKS verification (`IGoogleIdTokenVerifier`): issuer, audience, signature,
 expiry, `email_verified = true`.

Backend-driven OAuth was considered and rejected: state-cookie
 SameSite/CORS friction, per-environment redirect configuration,
 platform-specific mobile glue (ASWebAuthenticationSession, Android Custom
 Tabs). Client-driven leans on each platform's existing native SDK.

### JWKS verification is fail-closed

`GoogleIdTokenVerifier` goes through a typed `HttpClient` with Aspire's
 standard retry + circuit-breaker policies. Cached keys serve reads while
a refresh runs; refresh is triggered on `kid` miss and on 1-hour cache
 expiry. If JWKS is unreachable _and_ no cached key matches, verification
 fails and the endpoint returns 503. There is no code path that issues
 tokens when verification fails; asserted by an architectural test.
 Fail-open is never acceptable.

### Uniform email-loop

The only path that issues tokens on the initial `Login` submission is an
 already-linked `(provider, subject)`. Every other case — collision or
 first-time unknown email — creates a `PendingExternalLogin`, triggers an
 email, and returns an identical `202 Accepted` with
 `{ "status": "pending_confirmation" }`.

The email body differs (welcome vs. link-confirmation), but that signal
 lives in the victim's inbox, not the API response. The alternative —
 auto-provision instantly on unknown email, email-loop on collision —
 leaks account existence through response-shape differences and was
 rejected.

Collision lookup goes through `Email.Create(...)` for normalization
 (lowercase, trim) so `Alice@Example.Com` from Google matches an existing
 `alice@example.com`.

Provisioning happens on confirmation, not on initial submission. No
 `User` row is written until the user clicks the emailed link. This keeps
 the initial endpoint's behavior uniform and places the provisioning
 decision in the `Confirm` slice alone.

### Pending-record storage and concurrency

`PendingExternalLogin` stores already-verified Google claims (`Subject`,
 `Email`, `DisplayName`, `IsExistingUser`), the submitting client's IP and
User-Agent (for abuse investigation), and crucially _not_ the original
 `id_token`. Google id_tokens are valid for ~1 hour; storing them would
 couple confirmation-window UX to Google's token lifetime. Since we
 already verified the token at pending-record creation, re-verifying at
 confirm time would be both unnecessary and failure-prone.
 `IsExistingUser` is captured at creation time so the email template
 choice is deterministic and Notifications does not query the `users`
 table (which would cross a module boundary).

Confirmation is serialized with `SELECT … FOR UPDATE` on the pending
 row. Two concurrent `Confirm` requests for the same token cannot both
 succeed.

### Onboarding gate

Add `User.HasCompletedOnboarding` (bool). `CreateExternal` initializes to
`false`; `CreateWithPassword` initializes to `true`. The
 `CompleteOnboarding` slice flips the flag and records two distinct
 artefacts, described below. Existing users are backfilled to `true`.

Display name is seeded from Google's `name` claim at provision time.
 User-facing display-name editing is out of scope (future `UpdateProfile`
 slice).

### ToS acceptance and marketing consent — separate artefacts

ToS acceptance and marketing opt-in are distinct legal artefacts and are
modeled as such:

- **`TermsAcceptance` entity** (new, owned by `User`): records
  `(UserId, Version, AcceptedAt, AcceptedFromIp, UserAgent)`. Unique on
  `(UserId, Version)`. This is a _contract-agreement_ artefact under
  Art. 6(1)(b) — a record that the user agreed to the terms. It lives in
  its own table, not in `Consents`.
- **`Consents` registry** (existing, ADR-0012): strictly Art. 6(1)(a)
  opt-in consent. Used here for `marketing-emails` when the user opts
  in. `Consents` is extended in this phase with `granted_from_ip` and
  `granted_user_agent` columns so that demonstrability artefacts exist
  for every future grant.


`CompleteOnboarding` accepts
 `{ "acceptTerms": bool, "acceptMarketingEmails": bool }`. `acceptTerms`
 must be `true` — rejected with 400 otherwise. `acceptMarketingEmails`
 defaults to `false`. On success the handler:

1. Writes a `TermsAcceptance` row for the current
   `UsersOptions.TermsOfServiceVersion` (skipping if one already exists
   for that version).
2. If `acceptMarketingEmails = true`, grants a `marketing-emails`
   consent via `IConsentRegistry` with
   `UsersOptions.PrivacyPolicyVersion`, capturing IP + UA.
3. Flips `User.HasCompletedOnboarding = true`.


Re-running with `acceptMarketingEmails = false` does _not_ revoke a
 prior grant — revocation is an explicit separate action (out of scope).

### Lawful basis — per processing activity

For reviewer/regulator clarity, the Article 6 basis for each processing
 activity introduced by this phase:

| Activity                                                                               | Basis                                       | Notes                                                                                         |
| -------------------------------------------------------------------------------------- | ------------------------------------------- | --------------------------------------------------------------------------------------------- |
| Provisioning a `User` row on `Confirm`                                                 | Art. 6(1)(b) performance of contract        | The user's affirmative act (initiating Google sign-in + clicking the confirmation email) is a |
| request to enter a contract with us.                                                   |
| `PendingExternalLogin` storage (email, display name, IP, UA) before any account exists | Art. 6(1)(b) pre-contractual steps          | Storage is time-limited (≤30 min) and purpose-limited                                         |
| (complete the sign-in the user initiated).                                             |
| `ExternalLogin` row retention (subject, linked_at) after signup                        | Art. 6(1)(b) performance of contract        | The subject is necessary to re-authenticate the user.                                         |
| Capturing `CreatedFromIp` / `UserAgent` on pending records                             | Art. 6(1)(f) legitimate interest            | Abuse investigation on a mail-amplification endpoint. Proportionality protected by            |
| tight retention.                                                                       |
| Sending transactional emails (confirmation, linked/unlinked alerts)                    | Art. 6(1)(b) / 6(1)(f)                      | Non-opt-out per ADR-0014; necessary for account operation and security.                       |
| Audit trail entries for external-login events                                          | Art. 6(1)(f) legitimate interest            | Security and operational provenance.                                                          |
| `TermsAcceptance` records                                                              | Art. 6(1)(b) performance of contract        | Demonstrability of the agreement itself.                                                      |
| `Consents(marketing-emails)` rows                                                      | Art. 6(1)(a) consent                        | Explicit opt-in only. Revocable.                                                              |
| Processing Google-returned `email` and `name`                                          | Art. 6(1)(b) + Google's upstream consent UI | We rely on Google's OAuth consent screen as the user's affirmative act to share this          |
| data with us. Once collected and tied to a contract, retention shifts to 6(1)(b).      |

### Provisioning-channel event split

`UserRegistered` / `UserRegisteredV1` narrows to password registration
 only. External provisioning fires `UserProvisionedFromExternalV1`
 instead, carrying
 `(UserId, Provider, Subject, Email, DisplayName, ProvisionedAt)`. Audit
 and Notifications discriminate on event type. Notifications sends no
 welcome email on `UserProvisionedFromExternalV1`; the
 `ExternalLoginPendingV1` confirmation email already served that purpose.

This is a semantic narrowing of `UserRegisteredV1`. In-tree subscribers
 (Notifications) must be updated. Future external consumers inherit the
 same requirement.

### Unlink revokes sessions

Unlinking an external login revokes all of the user's refresh tokens —
 same mechanism as the sensitive events in ADR-0028. If a user unlinks
 because the provider account was compromised, any refresh tokens already
 issued via that provider are closed. ADR-0028's sensitive-event table is
 updated accordingly.

### Account linking from authenticated sessions

`POST /v1/users/me/external-logins/google` with no email loop — the
 active session already proves identity. Linking the same
 `(provider, subject)` to a second user returns 409.

### Mail amplification mitigations

- Coalesce: one active `PendingExternalLogin` per `(provider, subject)`.
- Per-IP rate limits via the existing `auth` policy (ADR-0018).
- Per-email cap on concurrent unconsumed pending records (default 3).
- Per-record IP + UA captured for forensic investigation.


### Retention, erasure, and export

Pending records are swept on `expires_at < now` — no grace period.
 Pending records have no audit value past expiry; data minimization
dominates.

Erasure additionally deletes `pending_external_logins` rows matching the
 erased user's email (normalized) — beyond the `User` FK cascade — to
 cover abandoned attempts and hostile submissions using the subject's
 email.

Export includes linked providers, `HasCompletedOnboarding`,
 `TermsAcceptance` history (version + IP + UA), and marketing consents
 (now including IP + UA). Pending records are excluded from export:
 they represent pre-account attempts the subject has no `User` for yet,
and including them by email would turn export into a partial
 email-enumeration tool against the pending table.

### Boundary with ADR-0007

ADR-0007 still holds. No ASP.NET Identity. Phase 14 adds one verifier,
 three tables (`external_logins`, `pending_external_logins`,
`terms_acceptances`), two columns on `consents`, and six feature slices.

### What this ADR does not address

- **Re-consent on ToS version bump for existing users.** Bumping
  `TermsOfServiceVersion` produces a fresh `TermsAcceptance` only on the
  next `CompleteOnboarding` run. Already-onboarded users are not prompted
  to re-accept. A cross-cutting "prompt-for-re-acceptance" mechanism is
  deferred to a future ADR.
- **Age / minor consent.** GDPR's lower threshold (13–16 depending on
  member state) requires parental consent. Google Sign-in does not
  expose the user's age. If the product scope includes minors, this
  becomes a separate design problem.
- **Cross-border transfer.** Google is a US processor. Transfer
  safeguards (SCCs, adequacy decisions) are operator responsibility and
  a privacy-policy concern, not a Phase 14 decision.


## Consequences

### Easier

- Adding further providers follows the Google template: new verifier,
  new enum value, parallel slice folders. No schema changes, no new
  cross-cutting infrastructure.
- Frontend and mobile clients use each platform's native SDK without
  platform-specific callback plumbing on our side.
- Account enumeration via the Google-login endpoint is closed.
- Mixed-credential accounts are first-class.
- Provenance questions answered by event type alone.
- ToS acceptance and marketing consent are cleanly separated; audit
  queries and regulator responses don't need to interpret what a
  "TermsOfService" entry in `Consents` would have meant.
- Both artefacts carry demonstrability context (IP, UA, timestamp,
  version) — the minimum for showing "how and when did the user agree."


### Harder

- First-time federated login has more friction than "click Google, get
  logged in." Accepted in exchange for enumeration resistance.
- `User.PasswordHash` is nullable — every password-touching flow
  (`ChangePassword`, `ResetPassword`, GDPR exporter, admin views) needs
  review.
- `UserRegisteredV1` semantics narrow. In-tree that's Notifications;
  future external consumers inherit the requirement.
- The `Login` endpoint is an email-amplification surface. Coalescing,
  rate limits, per-email caps, IP/UA capture contain it but add moving
  parts.
- Notifications gains two new templates per provider; drift is a real
  risk. Shared base template advisable.
- The `Confirm` slice owns the provisioning decision. Future policy
  changes (admin approval, allow/deny-lists, SSO mapping) land here.
- `PendingExternalLogin` is short-lived; the sweep is extended from
  `SweepExpiredTokens` — one more reason that job must not silently
  fail.
- The Users eraser reaches outside the `User` FK cascade to clean
  pending records by email. Future tables carrying email before a
  `User` exists must add their own eraser hooks.
- Two distinct artefact tables (`terms_acceptances`, `consents`) where
  a single table would be simpler. The split is deliberate — legal
  bases differ — but it costs one extra table and a clear naming
  convention in handlers.


### Neutral but worth recording

- We verify id_tokens only. We do not exchange authorization codes for
  Google refresh tokens and do not call Google APIs. If we ever need
  offline access, that's a separate decision.
- The subject (`sub`) is a stable per-app identifier. Users who revoke
  and re-grant our OAuth consent keep the same `sub`; users who switch
  Google client IDs do not. Rotating `ClientId` without planning for
  `sub` continuity is a breaking change.
- Automatic merging of duplicate accounts that share an email is not
  supported. Operators or the user resolve this manually.
- A Google-only user whose Google account is deleted or suspended stays
  logged in with us until their refresh token expires. Storing Google
  refresh tokens would surface this sooner but not fix it; the real
  mitigation is encouraging external-only users to add a password via
  `SetInitialPassword`.
- `Consents.GrantedFromIp` / `GrantedUserAgent` are added as part of
  this phase's migration. Any prior consent rows are backfilled to
  `NULL`. For already-granted consents we have no way to reconstruct
  this context retroactively; this is accepted.
