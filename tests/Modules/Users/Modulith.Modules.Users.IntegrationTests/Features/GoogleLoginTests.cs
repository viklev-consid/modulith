using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Features.ExternalLogin.Google.Login;
using Modulith.Modules.Users.Features.Login;
using Modulith.Modules.Users.Features.Register;
using Modulith.Modules.Users.Persistence;
using Modulith.Shared.Kernel.Interfaces;
using Modulith.TestSupport;

namespace Modulith.Modules.Users.IntegrationTests.Features;

[Collection("GoogleUsersModule")]
[Trait("Category", "Integration")]
public sealed class GoogleLoginTests(GoogleUsersApiFixture fixture) : IAsyncLifetime
{
    private readonly HttpClient _client = fixture.CreateAnonymousClient();

    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GoogleLogin_WhenNoAccountLinked_NewUser_Returns202()
    {
        fixture.GoogleVerifier.SetIdentity("sub-new", "brand-new@example.com", "New User");

        var response = await _client.PostAsJsonAsync("/v1/users/auth/google/login",
            new GoogleLoginRequest("any-id-token"));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("pending_confirmation", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task GoogleLogin_WhenEmailRegisteredButNotLinked_Returns202()
    {
        const string email = "existingnotlinked@example.com";
        await _client.PostAsJsonAsync("/v1/users/register",
            new RegisterRequest(email, "Password1!", "Alice"));

        fixture.GoogleVerifier.SetIdentity("sub-unlinked", email, "Alice");

        var response = await _client.PostAsJsonAsync("/v1/users/auth/google/login",
            new GoogleLoginRequest("any-id-token"));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("pending_confirmation", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task GoogleLogin_WhenGoogleAccountAlreadyLinked_Returns200WithTokens()
    {
        const string email = "fastpath@example.com";
        const string subject = "sub-fastpath";

        // Seed: register user and link Google directly via domain
        await _client.PostAsJsonAsync("/v1/users/register",
            new RegisterRequest(email, "Password1!", "Alice"));

        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            var emailVal = Email.Create(email).Value;
            var user = await db.Users
                .Include(u => u.ExternalLogins)
                .FirstAsync(u => u.Email == emailVal);
            user.LinkExternalLogin(ExternalLoginProvider.Google, subject, clock.UtcNow);
            await db.SaveChangesAsync();
        }

        fixture.GoogleVerifier.SetIdentity(subject, email, "Alice");

        var response = await _client.PostAsJsonAsync("/v1/users/auth/google/login",
            new GoogleLoginRequest("any-id-token"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<GoogleLoginResponse>();
        Assert.NotNull(body);
        Assert.False(body.IsPending);
        Assert.NotNull(body.AccessToken);
        Assert.NotNull(body.RefreshToken);
    }

    [Fact]
    public async Task GoogleLogin_WhenTokenVerificationFails_Returns401()
    {
        fixture.GoogleVerifier.SetError(Error.Unauthorized("Users.ExternalLogin.InvalidIdToken", "invalid"));

        var response = await _client.PostAsJsonAsync("/v1/users/auth/google/login",
            new GoogleLoginRequest("bad-token"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GoogleLogin_WhenIdTokenEmpty_Returns422()
    {
        var response = await _client.PostAsJsonAsync("/v1/users/auth/google/login",
            new GoogleLoginRequest(""));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task GoogleLogin_WhenIdTokenExceedsMaxLength_Returns422()
    {
        var oversized = new string('a', 4097);

        var response = await _client.PostAsJsonAsync("/v1/users/auth/google/login",
            new GoogleLoginRequest(oversized));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task GoogleLogin_EmailLoop_CreatesPendingRecordInDatabase()
    {
        fixture.GoogleVerifier.SetIdentity("sub-pending", "pending@example.com", "Pending User");

        await _client.PostAsJsonAsync("/v1/users/auth/google/login",
            new GoogleLoginRequest("any-id-token"));

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        var pending = await db.PendingExternalLogins
            .FirstOrDefaultAsync(p => p.Email == "pending@example.com");
        Assert.NotNull(pending);
        Assert.False(pending.IsExistingUser);
    }

    [Fact]
    public async Task GoogleLogin_WhenDisplayNameExceedsColumnLimit_TruncatesAndReturns202()
    {
        var overlong = new string('A', 150);
        fixture.GoogleVerifier.SetIdentity("sub-longname", "longname@example.com", overlong);

        var response = await _client.PostAsJsonAsync("/v1/users/auth/google/login",
            new GoogleLoginRequest("any-id-token"));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("pending_confirmation", body.GetProperty("status").GetString());

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        var pending = await db.PendingExternalLogins
            .FirstOrDefaultAsync(p => p.Email == "longname@example.com");
        Assert.NotNull(pending);
        Assert.Equal(100, pending.DisplayName.Length);
    }

    [Fact]
    public async Task GoogleLogin_WhenExpiredUnconsumedPendingExists_RefreshesAndReturns202()
    {
        // An expired-but-unconsumed pending row blocks any INSERT via the partial unique index
        // (provider, subject) WHERE consumed_at IS NULL. The handler must refresh it rather than
        // attempt a new insert, otherwise the catch block silently swallows the constraint violation
        // and no email is sent.
        const string subject = "sub-expired-pending";
        const string email = "expired-pending@example.com";

        fixture.GoogleVerifier.SetIdentity(subject, email, "Expired User");

        // Seed an already-expired unconsumed pending login directly.
        var pastClock = new TestClock();
        pastClock.Set(DateTimeOffset.UtcNow.AddHours(-2));

        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
            var (expiredPending, _) = PendingExternalLogin.Create(
                ExternalLoginProvider.Google,
                subject,
                email,
                "Expired User",
                isExistingUser: false,
                createdFromIp: null,
                userAgent: null,
                lifetime: TimeSpan.FromMinutes(15),
                clock: pastClock);   // issued 2 h ago, expired 1 h 45 min ago
            db.PendingExternalLogins.Add(expiredPending);
            await db.SaveChangesAsync();
        }

        var response = await _client.PostAsJsonAsync("/v1/users/auth/google/login",
            new GoogleLoginRequest("any-id-token"));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("pending_confirmation", body.GetProperty("status").GetString());

        // The handler must have refreshed the row (not silently swallowed the constraint):
        // exactly one unconsumed row exists and it has a future expiry.
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            var pending = await db.PendingExternalLogins
                .SingleOrDefaultAsync(p =>
                    p.Provider == ExternalLoginProvider.Google &&
                    p.Subject == subject &&
                    p.ConsumedAt == null);
            Assert.NotNull(pending);
            Assert.True(pending.ExpiresAt > clock.UtcNow,
                "Refreshed row must have a future expiry, not the original expired one.");
        }
    }

    [Fact]
    public async Task GoogleLogin_AfterUnlink_FallsBackToEmailLoop()
    {
        const string email = "postunlink@example.com";
        const string subject = "sub-postunlink";

        // Register a user with a password so the credential-retention guardrail allows unlink.
        var regResp = await _client.PostAsJsonAsync("/v1/users/register",
            new RegisterRequest(email, "Password1!", "Alice"));
        var regBody = await regResp.Content.ReadFromJsonAsync<RegisterResponse>();
        Assert.NotNull(regBody);

        // Seed Google as a linked provider directly in the DB.
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            var user = await db.Users
                .Include(u => u.ExternalLogins)
                .FirstAsync(u => u.Id == new UserId(regBody.UserId));
            user.LinkExternalLogin(ExternalLoginProvider.Google, subject, clock.UtcNow);
            await db.SaveChangesAsync();
        }

        // Confirm the fast path works before unlink.
        fixture.GoogleVerifier.SetIdentity(subject, email, "Alice");
        var fastPathBefore = await _client.PostAsJsonAsync("/v1/users/auth/google/login",
            new GoogleLoginRequest("any-id-token"));
        Assert.Equal(HttpStatusCode.OK, fastPathBefore.StatusCode);

        // Obtain a session token to authenticate the unlink request.
        var loginResp = await _client.PostAsJsonAsync("/v1/users/login",
            new LoginRequest(email, "Password1!"));
        var loginBody = await loginResp.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(loginBody);
        var auth = fixture.CreateAuthenticatedClientWithToken(loginBody.AccessToken);

        // Unlink Google — revokes all sessions and deletes the ExternalLogin row.
        var unlinkResp = await auth.DeleteAsync("/v1/users/me/auth/google/unlink");
        Assert.Equal(HttpStatusCode.NoContent, unlinkResp.StatusCode);

        // The fast path must now be dead: the ExternalLogin row is gone, so login must
        // fall through to the email loop and return 202 instead of 200 with tokens.
        fixture.GoogleVerifier.SetIdentity(subject, email, "Alice");
        var fastPathAfter = await _client.PostAsJsonAsync("/v1/users/auth/google/login",
            new GoogleLoginRequest("any-id-token"));

        Assert.Equal(HttpStatusCode.Accepted, fastPathAfter.StatusCode);
        var body = await fastPathAfter.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("pending_confirmation", body.GetProperty("status").GetString());
    }
}
