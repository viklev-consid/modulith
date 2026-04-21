# How-to: Implement Auth Flows

The Users module ships with a baseline set of authentication flows: password reset, password change, email change, logout, and refresh token rotation. This guide walks through their shape and the security invariants to preserve when modifying them.

For the decisions, see [`../adr/0028-auth-flows-baseline.md`](../adr/0028-auth-flows-baseline.md) and [`../adr/0029-refresh-tokens-and-logout.md`](../adr/0029-refresh-tokens-and-logout.md).

---

## The slices at a glance

| Slice | Endpoint | Auth | Rate limit |
|---|---|---|---|
| Register | `POST /v1/users/register` | public | `auth` |
| Login | `POST /v1/users/login` | public | `auth` |
| RefreshToken | `POST /v1/users/token/refresh` | public* | `auth` |
| Logout | `POST /v1/users/logout` | authenticated | `write` |
| LogoutAll | `POST /v1/users/logout/all` | authenticated | `write` |
| GetCurrentUser | `GET /v1/users/me` | authenticated | `read` |
| ChangePassword | `POST /v1/users/me/password` | authenticated | `auth` |
| RequestEmailChange | `POST /v1/users/me/email/request` | authenticated | `write` |
| ConfirmEmailChange | `POST /v1/users/me/email/confirm` | authenticated | `auth` |
| ForgotPassword | `POST /v1/users/password/forgot` | public | `auth` |
| ResetPassword | `POST /v1/users/password/reset` | public | `auth` |

\*Refresh endpoint is technically public because the access token has expired; authentication happens via the refresh token itself.

---

## The core primitives

### SingleUseToken value object

Used for password reset and email change. Lives in the Users domain.

```csharp
public sealed record SingleUseToken
{
    public required byte[] TokenHash { get; init; }
    public required TokenPurpose Purpose { get; init; }
    public required UserId UserId { get; init; }
    public required DateTimeOffset IssuedAt { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset? ConsumedAt { get; private set; }

    public bool IsValid(IClock clock) =>
        ConsumedAt is null && ExpiresAt > clock.UtcNow;

    public static (SingleUseToken token, string rawValue) Create(
        UserId userId, TokenPurpose purpose, TimeSpan lifetime, IClock clock)
    {
        var raw = GenerateRandomTokenString();        // 256-bit, URL-safe base64
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        var token = new SingleUseToken
        {
            TokenHash = hash,
            Purpose = purpose,
            UserId = userId,
            IssuedAt = clock.UtcNow,
            ExpiresAt = clock.UtcNow + lifetime
        };
        return (token, raw);
    }

    internal void Consume(IClock clock) => ConsumedAt = clock.UtcNow;
}

public enum TokenPurpose { PasswordReset, EmailChange }
```

Critical: `Create` returns both the hashed token (for storage) and the raw value (for sending to the user). The raw value is **never** stored.

### RefreshToken entity

```csharp
public sealed class RefreshToken : Entity<RefreshTokenId>
{
    public UserId UserId { get; private set; }
    public byte[] TokenHash { get; private set; }
    public DateTimeOffset IssuedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public RefreshTokenId? RotatedTo { get; private set; }
    public string? DeviceFingerprint { get; private set; }
    public IPAddress? CreatedFromIp { get; private set; }

    public bool IsActive(IClock clock) =>
        RevokedAt is null && ExpiresAt > clock.UtcNow;

    public bool WasRotated => RotatedTo is not null;

    internal void Revoke(IClock clock) => RevokedAt ??= clock.UtcNow;

    internal void MarkRotatedTo(RefreshTokenId newId, IClock clock)
    {
        Revoke(clock);
        RotatedTo = newId;
    }
}
```

---

## Password reset flow

### ForgotPassword slice

```csharp
// ForgotPassword.Request.cs
public sealed record ForgotPasswordRequest(string Email);

// ForgotPassword.Response.cs
public sealed record ForgotPasswordResponse(string Message);

// ForgotPassword.Command.cs
internal sealed record ForgotPasswordCommand(Email Email);

// ForgotPassword.Handler.cs
internal sealed class ForgotPasswordHandler
{
    private readonly UsersDbContext _db;
    private readonly IClock _clock;
    private readonly IMessageBus _bus;
    private readonly IOptions<UsersOptions> _options;

    public async Task<ErrorOr<ForgotPasswordResponse>> Handle(
        ForgotPasswordCommand cmd, CancellationToken ct)
    {
        var user = await _db.Users.SingleOrDefaultAsync(u => u.Email == cmd.Email, ct);

        // CRITICAL: return the same response whether the user exists or not
        if (user is not null)
        {
            var (token, rawValue) = SingleUseToken.Create(
                user.Id,
                TokenPurpose.PasswordReset,
                _options.Value.PasswordResetTokenLifetime,
                _clock);

            _db.SingleUseTokens.Add(token);
            await _db.SaveChangesAsync(ct);

            await _bus.PublishAsync(new PasswordResetRequestedV1(
                user.Id.Value,
                user.Email.Value,
                rawValue,                           // sent to Notifications, included in email
                token.ExpiresAt));
        }

        return new ForgotPasswordResponse(
            "If that email is registered, a password reset link has been sent.");
    }
}
```

**Security notes:**

- Response is identical regardless of whether the email exists.
- Branches should have comparable timing. The `user is not null` branch does extra work (token generation + DB write + event publish); the difference is measurable. If your threat model includes timing attacks, add compensating work (e.g., a dummy SHA-256 computation) in the else branch. For most apps, rate limiting on `auth` policy is the primary mitigation and timing is acceptable.
- `PasswordResetRequestedV1` carries the raw token only to the Notifications module, which embeds it in the email and never logs it.

### ResetPassword slice

```csharp
// ResetPassword.Request.cs
public sealed record ResetPasswordRequest(string Token, string NewPassword);

// ResetPassword.Handler.cs
internal sealed class ResetPasswordHandler
{
    public async Task<ErrorOr<ResetPasswordResponse>> Handle(
        ResetPasswordCommand cmd, CancellationToken ct)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(cmd.RawToken));

        var token = await _db.SingleUseTokens
            .SingleOrDefaultAsync(t =>
                t.TokenHash == hash && t.Purpose == TokenPurpose.PasswordReset, ct);

        if (token is null || !token.IsValid(_clock))
            return Errors.Users.InvalidOrExpiredToken;

        var user = await _db.Users.FindAsync([token.UserId], ct);
        if (user is null)
            return Errors.Users.InvalidOrExpiredToken;

        var passwordResult = user.SetPassword(cmd.NewPassword);
        if (passwordResult.IsError) return passwordResult.Errors;

        token.Consume(_clock);

        // Revoke all refresh tokens — password reset is a sensitive event
        await _db.RefreshTokens
            .Where(rt => rt.UserId == user.Id && rt.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(rt => rt.RevokedAt, _clock.UtcNow), ct);

        await _db.SaveChangesAsync(ct);

        // Publish for audit and for optional "your password was reset" email to old address
        return new ResetPasswordResponse("Password has been reset.");
    }
}
```

**Security notes:**

- Token lookup uses the SHA-256 hash of the incoming token. Unhashed storage would mean a stolen DB row = account takeover.
- `InvalidOrExpiredToken` is a single error — don't distinguish "invalid" from "expired" in the response. Consistent response prevents oracle attacks.
- All refresh tokens are revoked on reset. A reset means "I lost control of my account" and any existing sessions are suspect.

---

## Change password (authenticated)

```csharp
// ChangePassword.Request.cs
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

// ChangePassword.Handler.cs
internal sealed class ChangePasswordHandler
{
    public async Task<ErrorOr<ChangePasswordResponse>> Handle(
        ChangePasswordCommand cmd, CancellationToken ct)
    {
        var user = await _db.Users.FindAsync([cmd.UserId], ct);
        if (user is null) return Errors.Users.NotFound(cmd.UserId);

        if (!user.VerifyPassword(cmd.CurrentPassword))
            return Errors.Users.InvalidCredentials;

        var result = user.SetPassword(cmd.NewPassword);
        if (result.IsError) return result.Errors;

        // Revoke all refresh tokens EXCEPT the one used on this request
        var currentRefreshTokenId = _currentUser.CurrentRefreshTokenId;
        await _db.RefreshTokens
            .Where(rt => rt.UserId == user.Id
                && rt.RevokedAt == null
                && rt.Id != currentRefreshTokenId)
            .ExecuteUpdateAsync(s => s.SetProperty(rt => rt.RevokedAt, _clock.UtcNow), ct);

        await _db.SaveChangesAsync(ct);

        return new ChangePasswordResponse();
    }
}
```

**Security notes:**

- Current password is required. Prevents a stolen session from locking the real user out by changing the password silently.
- Constant-time password verification (BCrypt does this by default; don't roll your own comparison).
- The user's current session is preserved (their refresh token stays valid); all other sessions are revoked.

---

## Email change (two-step)

### RequestEmailChange slice

```csharp
internal sealed class RequestEmailChangeHandler
{
    public async Task<ErrorOr<RequestEmailChangeResponse>> Handle(
        RequestEmailChangeCommand cmd, CancellationToken ct)
    {
        var user = await _db.Users.FindAsync([cmd.UserId], ct);
        if (user is null) return Errors.Users.NotFound(cmd.UserId);

        if (!user.VerifyPassword(cmd.CurrentPassword))
            return Errors.Users.InvalidCredentials;

        // Email-uniqueness check — but don't leak which address is taken
        if (await _db.Users.AnyAsync(u => u.Email == cmd.NewEmail && u.Id != user.Id, ct))
            return Errors.Users.EmailChangeRequestReceived;  // generic success response

        var (token, rawValue) = SingleUseToken.Create(
            user.Id, TokenPurpose.EmailChange, _options.Value.EmailChangeTokenLifetime, _clock);

        // Store the requested new email alongside the token
        _db.PendingEmailChanges.Add(new PendingEmailChange(user.Id, cmd.NewEmail, token.Id));
        _db.SingleUseTokens.Add(token);

        await _db.SaveChangesAsync(ct);

        await _bus.PublishAsync(new EmailChangeRequestedV1(
            user.Id.Value, cmd.NewEmail.Value, rawValue, token.ExpiresAt));

        return new RequestEmailChangeResponse(
            "A confirmation link has been sent to the new email address.");
    }
}
```

### ConfirmEmailChange slice

```csharp
internal sealed class ConfirmEmailChangeHandler
{
    public async Task<ErrorOr<ConfirmEmailChangeResponse>> Handle(
        ConfirmEmailChangeCommand cmd, CancellationToken ct)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(cmd.RawToken));

        var token = await _db.SingleUseTokens
            .SingleOrDefaultAsync(t =>
                t.TokenHash == hash
                && t.Purpose == TokenPurpose.EmailChange
                && t.UserId == cmd.UserId, ct);

        if (token is null || !token.IsValid(_clock))
            return Errors.Users.InvalidOrExpiredToken;

        var pending = await _db.PendingEmailChanges
            .SingleOrDefaultAsync(p => p.TokenId == token.Id, ct);
        if (pending is null)
            return Errors.Users.InvalidOrExpiredToken;

        var user = await _db.Users.FindAsync([cmd.UserId], ct);
        var oldEmail = user!.Email;

        var result = user.ChangeEmail(pending.NewEmail);
        if (result.IsError) return result.Errors;

        token.Consume(_clock);
        _db.PendingEmailChanges.Remove(pending);

        // Revoke all refresh tokens
        await _db.RefreshTokens
            .Where(rt => rt.UserId == user.Id && rt.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(rt => rt.RevokedAt, _clock.UtcNow), ct);

        await _db.SaveChangesAsync(ct);

        // Publish so Notifications alerts the OLD email address of the change
        await _bus.PublishAsync(new EmailChangedV1(
            user.Id.Value, oldEmail.Value, user.Email.Value));

        return new ConfirmEmailChangeResponse();
    }
}
```

**Security notes:**

- Confirmation is *required* before the email actually changes. Unlike some implementations that change the stored email immediately and "verify later", this design does not allow the new email to become the login identifier until it's proven to belong to the user.
- The *old* email address receives an alert after the change completes. Defense-in-depth against silent account takeover.
- Token lookup also checks `UserId` — even if tokens collide (hash collision, extremely improbable), the user binding prevents cross-user token use.

---

## Refresh token rotation

### Login slice (updated from Phase 4)

```csharp
internal sealed class LoginHandler
{
    public async Task<ErrorOr<LoginResponse>> Handle(
        LoginCommand cmd, CancellationToken ct)
    {
        var user = await _db.Users.SingleOrDefaultAsync(u => u.Email == cmd.Email, ct);
        if (user is null) return Errors.Users.InvalidCredentials;

        if (!user.VerifyPassword(cmd.Password))
            return Errors.Users.InvalidCredentials;

        // Enforce MaxActiveRefreshTokensPerUser — revoke oldest if at limit
        await EnforceSessionLimit(user.Id, ct);

        var (refreshToken, rawRefreshValue) = RefreshToken.Issue(
            user.Id,
            _options.Value.RefreshTokenLifetime,
            _clock,
            _currentRequest.UserAgentFingerprint,
            _currentRequest.ClientIp);

        _db.RefreshTokens.Add(refreshToken);
        await _db.SaveChangesAsync(ct);

        var accessToken = _tokenIssuer.IssueAccessToken(user);

        return new LoginResponse(
            AccessToken: accessToken.Value,
            AccessTokenExpiresAt: accessToken.ExpiresAt,
            RefreshToken: rawRefreshValue,
            RefreshTokenExpiresAt: refreshToken.ExpiresAt);
    }
}
```

### RefreshToken slice

```csharp
// RefreshToken.Request.cs
public sealed record RefreshTokenRequest(string RefreshToken);

internal sealed class RefreshTokenHandler
{
    public async Task<ErrorOr<RefreshTokenResponse>> Handle(
        RefreshTokenCommand cmd, CancellationToken ct)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(cmd.RawToken));

        var existing = await _db.RefreshTokens
            .SingleOrDefaultAsync(rt => rt.TokenHash == hash, ct);

        if (existing is null)
            return Errors.Users.InvalidRefreshToken;

        // REUSE DETECTION: if the token is already rotated, treat as theft
        if (existing.WasRotated)
        {
            // Revoke the entire chain + force re-login
            await RevokeAllForUser(existing.UserId, ct);
            await _db.SaveChangesAsync(ct);
            return Errors.Users.RefreshTokenReuseDetected;
        }

        if (!existing.IsActive(_clock))
            return Errors.Users.InvalidRefreshToken;

        var user = await _db.Users.FindAsync([existing.UserId], ct);
        if (user is null || !user.IsActive)
            return Errors.Users.InvalidRefreshToken;

        // Issue new refresh token
        var (newToken, rawNewValue) = RefreshToken.Issue(
            existing.UserId,
            _options.Value.RefreshTokenLifetime,
            _clock,
            _currentRequest.UserAgentFingerprint,
            _currentRequest.ClientIp);

        _db.RefreshTokens.Add(newToken);
        existing.MarkRotatedTo(newToken.Id, _clock);

        await _db.SaveChangesAsync(ct);

        var accessToken = _tokenIssuer.IssueAccessToken(user);

        return new RefreshTokenResponse(
            AccessToken: accessToken.Value,
            AccessTokenExpiresAt: accessToken.ExpiresAt,
            RefreshToken: rawNewValue,
            RefreshTokenExpiresAt: newToken.ExpiresAt);
    }
}
```

**Security notes:**

- **Reuse detection is not optional.** If a refresh token is presented twice, one of the presenters isn't the real client. Revoking the entire chain forces both parties to re-login; the real client notices a forced logout and re-authenticates, while the attacker's stolen token becomes useless.
- Concurrent-refresh race: two simultaneous requests with the same valid refresh token may both succeed if the DB doesn't serialize. Address this with `SELECT ... FOR UPDATE` in the lookup or with optimistic concurrency on `RevokedAt`. The template uses serializable read (via a pessimistic lock helper) — documented in the implementation.
- Access token is issued fresh every time; never reused across refreshes.

---

## Logout

### Logout slice

```csharp
internal sealed class LogoutHandler
{
    public async Task<ErrorOr<LogoutResponse>> Handle(
        LogoutCommand cmd, CancellationToken ct)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(cmd.RawRefreshToken));

        var token = await _db.RefreshTokens.SingleOrDefaultAsync(
            rt => rt.TokenHash == hash && rt.UserId == cmd.UserId, ct);

        // Silent success if token already gone — don't leak presence
        if (token is not null && token.IsActive(_clock))
        {
            token.Revoke(_clock);
            await _db.SaveChangesAsync(ct);
        }

        return new LogoutResponse();
    }
}
```

### LogoutAll slice

```csharp
internal sealed class LogoutAllHandler
{
    public async Task<ErrorOr<LogoutAllResponse>> Handle(
        LogoutAllCommand cmd, CancellationToken ct)
    {
        var revoked = await _db.RefreshTokens
            .Where(rt => rt.UserId == cmd.UserId && rt.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(rt => rt.RevokedAt, _clock.UtcNow), ct);

        await _bus.PublishAsync(new UserLoggedOutAllDevicesV1(cmd.UserId.Value, revoked));

        return new LogoutAllResponse(RefreshTokensRevoked: revoked);
    }
}
```

---

## Client integration notes

The access/refresh split requires client-side refresh logic. Canonical pattern (HTTP interceptor):

1. On 401 from any endpoint, attempt `POST /v1/users/token/refresh` with the stored refresh token.
2. If refresh succeeds, retry the original request with the new access token.
3. If refresh fails (including reuse detection), treat as logged out — redirect to login.
4. Serialize concurrent refreshes — if two requests both get 401 simultaneously, only the first should refresh; the second waits for the result.

For web clients storing refresh tokens:

- **Preferred:** `HttpOnly; Secure; SameSite=Strict` cookie set by the login endpoint. The refresh endpoint reads the cookie. No JS access — immune to XSS token theft.
- **Acceptable:** `localStorage`, with awareness that XSS compromises the token. Strong CSP required.
- **Never:** URL query parameters. Logged everywhere.

The template's reference client guidance document these patterns in `docs/how-to/client-integration.md` (added in Phase 4.5).

---

## Testing these flows

Representative integration tests:

**Password reset happy path:**

```csharp
[Fact]
public async Task ForgotPasswordThenReset_AllowsLoginWithNewPassword()
{
    var user = await fixture.SeedAsync(UserMother.WithPassword("old-password"));

    // Step 1: request reset
    await client.PostAsJsonAsync("/v1/users/password/forgot",
        new ForgotPasswordRequest(user.Email.Value));

    // Capture the token from the published event
    var token = fixture.Tracker.Published<PasswordResetRequestedV1>()
        .Single().RawToken;

    // Step 2: reset
    var resetResp = await client.PostAsJsonAsync("/v1/users/password/reset",
        new ResetPasswordRequest(token, "new-password"));
    resetResp.StatusCode.ShouldBe(HttpStatusCode.OK);

    // Step 3: old password fails
    var failedLogin = await client.PostAsJsonAsync("/v1/users/login",
        new LoginRequest(user.Email.Value, "old-password"));
    failedLogin.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

    // Step 4: new password succeeds
    var ok = await client.PostAsJsonAsync("/v1/users/login",
        new LoginRequest(user.Email.Value, "new-password"));
    ok.StatusCode.ShouldBe(HttpStatusCode.OK);
}
```

**Enumeration resistance:**

```csharp
[Fact]
public async Task ForgotPassword_ReturnsSameResponseForUnknownEmail()
{
    await fixture.SeedAsync(UserMother.WithEmail("known@example.com"));

    var r1 = await client.PostAsJsonAsync("/v1/users/password/forgot",
        new ForgotPasswordRequest("known@example.com"));
    var r2 = await client.PostAsJsonAsync("/v1/users/password/forgot",
        new ForgotPasswordRequest("unknown@example.com"));

    r1.StatusCode.ShouldBe(HttpStatusCode.OK);
    r2.StatusCode.ShouldBe(HttpStatusCode.OK);
    (await r1.Content.ReadAsStringAsync())
        .ShouldBe(await r2.Content.ReadAsStringAsync());
}
```

**Refresh token rotation + reuse detection:**

```csharp
[Fact]
public async Task UsingRevokedRefreshToken_RevokesEntireChain()
{
    var user = await fixture.SeedAsync(UserMother.Active());
    var login = await Login(user);
    var firstRefresh = login.RefreshToken;

    // Use once — rotation
    var r1 = await Refresh(firstRefresh);
    r1.StatusCode.ShouldBe(HttpStatusCode.OK);

    // Use the OLD token again — should be detected as reuse
    var r2 = await Refresh(firstRefresh);
    r2.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

    // The NEW token should now also be revoked (entire chain)
    var newToken = await r1.Content.ReadFromJsonAsync<LoginResponse>();
    var r3 = await Refresh(newToken!.RefreshToken);
    r3.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
}
```

**Sensitive event revokes sessions:**

```csharp
[Fact]
public async Task PasswordReset_RevokesAllRefreshTokens()
{
    var user = await fixture.SeedAsync(UserMother.Active());
    var session1 = await Login(user);
    var session2 = await Login(user);

    await DoPasswordReset(user.Email);

    // Both sessions' refresh tokens are revoked
    (await Refresh(session1.RefreshToken)).StatusCode
        .ShouldBe(HttpStatusCode.Unauthorized);
    (await Refresh(session2.RefreshToken)).StatusCode
        .ShouldBe(HttpStatusCode.Unauthorized);
}
```

---

## Common mistakes

- **Storing raw tokens.** Always store hashes. Even with a perfect access model, a DB dump shouldn't be an authentication compromise.
- **Distinguishing "email not found" from "wrong password" in login errors.** Use `InvalidCredentials` for both.
- **Distinguishing "invalid token" from "expired token" in reset/confirm errors.** Use `InvalidOrExpiredToken` for both.
- **Forgetting to revoke refresh tokens on password change.** The attacker keeps their session.
- **Changing email before confirmation.** Lets an attacker lock the real user out.
- **Not alerting the old email on email change.** Silent account takeover.
- **Not enforcing max sessions per user.** Unbounded refresh-token table growth.
- **Not sweeping expired tokens.** Same issue.
- **Not implementing reuse detection on refresh.** Stolen refresh tokens go unnoticed until they expire naturally.
- **Using `DateTime.UtcNow` instead of `IClock`.** Tests can't control time; security invariants become hard to verify.

---

## Related

- [`../adr/0007-no-aspnet-identity.md`](../adr/0007-no-aspnet-identity.md)
- [`../adr/0028-auth-flows-baseline.md`](../adr/0028-auth-flows-baseline.md)
- [`../adr/0029-refresh-tokens-and-logout.md`](../adr/0029-refresh-tokens-and-logout.md)
- [`../adr/0018-rate-limiting.md`](../adr/0018-rate-limiting.md)
- [`../adr/0014-notifications-architecture.md`](../adr/0014-notifications-architecture.md)
- [`handle-failures.md`](handle-failures.md)
