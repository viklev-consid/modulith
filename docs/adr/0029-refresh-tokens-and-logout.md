# ADR-0029: Refresh Tokens for Logout and Session Management

## Status

Accepted

## Context

Pure JWT authentication is stateless: the server validates a signature and reads claims. There is no server-side session. "Logout" in this model is purely client-side — delete the token, done. Nothing to do on the server.

This is fine for some systems and actively wrong for others. The tensions:

- **Blast radius of a leaked token.** A stolen stateless JWT is valid until expiry. Short lifetimes limit exposure but hurt UX (users re-login constantly).
- **No revocation.** Suspected compromise (stolen device, password change) cannot invalidate outstanding tokens until they expire naturally.
- **No "logout everywhere".** Users expect this.

Three honest options:

1. **Pure stateless JWTs, client-side logout only.** Simplest. Relies on short token lifetimes. No revocation. Works for internal tools and low-stakes APIs. Fails the "logout everywhere" expectation.

2. **Refresh token rotation with server-side revocation.** Access tokens stay stateless and short-lived (minutes). Refresh tokens are long-lived (days/weeks), persisted server-side, revokable. Every access-token refresh rotates the refresh token. This is the mainstream pattern in modern consumer APIs (Auth0, Okta, Firebase, etc.).

3. **Full session tracking with revocation list.** Every access token has a `jti` claim checked against a revocation list on every request. Correct but gives up stateless auth for the hot path (DB/cache hit per request). Worth it for banking, healthcare, or systems with very high revocation stakes.

Option 1 is too minimal for a general-purpose template. Option 3 is overkill. Option 2 is the widely-accepted sweet spot.

## Decision

Use refresh tokens with rotation. Access tokens stay stateless; refresh tokens are the revocation handle.

### Token shapes

**Access token:** standard JWT, 15 minutes lifetime, stateless validation via signing key. No DB lookup on validation.

**Refresh token:** opaque, random 256-bit value, URL-safe base64 encoded. Stored server-side. 30 days lifetime by default, configurable.

### RefreshToken aggregate/entity

Lives in the `users` schema:

```
refresh_tokens
  id                  uuid primary key
  user_id             uuid (indexed, FK to users.users logically)
  token_hash          bytea (SHA-256 of the raw token)
  issued_at           timestamptz
  expires_at          timestamptz (indexed)
  revoked_at          timestamptz nullable
  rotated_to          uuid nullable (the ID of the replacement token)
  device_fingerprint  text nullable (UA + IP hash, opaque; for "logout everywhere" context)
  created_from_ip     inet nullable
```

**Never store the raw token.** The raw token is returned to the client once at issuance; only the SHA-256 hash is persisted. A DB dump does not leak usable tokens.

SHA-256 is appropriate here (not BCrypt). The raw token is already high-entropy (256 bits); key-stretching is unnecessary and would make validation pointlessly slow for a hot-path operation.

### Issuance

The Login slice returns both:

```json
{
  "accessToken": "<jwt>",
  "accessTokenExpiresAt": "2026-04-20T12:15:00Z",
  "refreshToken": "<opaque>",
  "refreshTokenExpiresAt": "2026-05-20T12:00:00Z"
}
```

### Rotation on refresh

`POST /v1/users/token/refresh` with `{ refreshToken }`:

1. Hash the provided token, look up the record.
2. Verify: not revoked, not expired, belongs to an active user.
3. Mark the old record as revoked (`revoked_at = now`, `rotated_to = <new id>`).
4. Issue a new refresh token and a new access token.
5. Return both.

**Reuse detection:** if an already-rotated refresh token is presented (i.e., `revoked_at IS NOT NULL AND rotated_to IS NOT NULL`), treat it as a theft indicator. Revoke the entire rotation chain and force re-login. This is the standard defense against stolen refresh tokens — the legitimate client will notice their refresh failed and re-authenticate.

### Logout

`POST /v1/users/logout` — authenticated, takes the refresh token (either in body or derived from a cookie, depending on client model):

- Revokes that one refresh token.
- Access token remains valid until natural expiry (up to 15 min).

The short access-token lifetime makes this acceptable. Clients should discard the access token client-side as well.

`POST /v1/users/logout/all` — authenticated:

- Revokes every refresh token belonging to the user.
- Publishes `UserLoggedOutAllDevices`.
- Access tokens continue to expire naturally within 15 min.

### Revocation on sensitive events

ADR-0028 establishes that these events revoke refresh tokens:

- **Password reset**: revoke all.
- **Password change**: revoke all except the one on the current request.
- **Email change**: revoke all.

These are handled inside the respective slices — not via integration events — to ensure the revocation is transactional with the state change.

### Sweeping expired tokens

A scheduled Wolverine job runs daily, deleting records where `expires_at < now - <grace period>`. The grace period (e.g., 7 days past expiry) retains tokens briefly for audit/forensics.

### Device fingerprint

Optional, populated from `User-Agent` + a hash of the client IP. Opaque — purely informational, shown to users on the "active sessions" list. Not used for authentication (IPs change, UAs change). Do not rely on it for security decisions.

### Configuration

```csharp
public sealed class UsersOptions
{
    // ... other options

    [Range(1, 1440)]
    public int AccessTokenLifetimeMinutes { get; init; } = 15;

    [Range(1, 365)]
    public int RefreshTokenLifetimeDays { get; init; } = 30;

    [Range(1, 100)]
    public int MaxActiveRefreshTokensPerUser { get; init; } = 10;
}
```

The `MaxActiveRefreshTokensPerUser` limits session sprawl — when a user logs in and they already have the max active tokens, the oldest one is revoked. Prevents unbounded growth from old devices.

### What this is NOT

- **Not a session store for access tokens.** Access tokens remain stateless. Revocation is via refresh — the 15-min window is the accepted trade-off.
- **Not "sliding session" behavior beyond rotation.** The refresh token has a hard expiry at issuance time + lifetime. Rotation creates a new token with a new expiry, but no token extends its own life.
- **Not a solution for concurrent-use prevention.** Allowing multiple concurrent sessions is the default; if a team needs "one device at a time" they need a different design.

## Consequences

**Positive:**

- Standard, well-understood pattern. Aligns with Auth0 / Okta / Firebase / OAuth 2.0 expectations.
- Access-token validation stays stateless and fast — no DB hit per request.
- Revocation is possible (15-min worst case before naturally invalidated).
- "Logout everywhere" works.
- Reuse detection gives a real defense against stolen refresh tokens.
- Short access-token lifetime limits exposure without ruining UX, because refresh is automatic.

**Negative:**

- More complexity in the Users module. Accepted.
- Clients must implement refresh logic (typically in an HTTP interceptor). Documented in `docs/how-to/` and in a client implementation note.
- Up to 15 minutes of stale authorization after revocation. Acceptable for the template's target use cases; teams with stricter needs can shorten the access-token lifetime or move to Option 3.
- Refresh tokens must be stored by the client. For web clients, `HttpOnly; Secure; SameSite=Strict` cookies are recommended over localStorage; the template documents both patterns.
- Rotation edge cases (concurrent requests both trying to refresh with the same token) require careful handling. Addressed by the reuse-detection logic.

## Related

- ADR-0007 (No ASP.NET Identity): the lightweight auth foundation.
- ADR-0028 (Auth Flows Baseline): refresh-token revocation is used by the sensitive-event flows.
- ADR-0011 (Auditing): login/logout/refresh events flow through the audit module.
- ADR-0018 (Rate Limiting): `POST /v1/users/token/refresh` uses the `auth` policy.
