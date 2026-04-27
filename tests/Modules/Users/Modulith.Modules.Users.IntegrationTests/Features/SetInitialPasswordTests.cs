using System.Net;
using System.Net.Http.Json;
using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Features.ExternalLogin.Google.Confirm;
using Modulith.Modules.Users.Features.RefreshToken;
using Modulith.Modules.Users.Persistence;
using Modulith.Shared.Kernel.Interfaces;
using Modulith.TestSupport;

namespace Modulith.Modules.Users.IntegrationTests.Features;

[Collection("GoogleUsersModule")]
[Trait("Category", "Integration")]
public sealed class SetInitialPasswordTests(GoogleUsersApiFixture fixture) : IAsyncLifetime
{
    private readonly HttpClient _anon = fixture.CreateAnonymousClient();

    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SetInitialPassword_ForExternalOnlyUser_Returns204()
    {
        var (_, accessToken) = await SeedExternalUserAsync("setpwd@example.com");
        var auth = fixture.CreateAuthenticatedClientWithToken(accessToken);

        var response = await auth.PostAsJsonAsync("/v1/users/me/password/initial",
            new { password = "NewPassword1!", googleIdToken = "fake-google-token" });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task SetInitialPassword_PersistsPasswordHashInDatabase()
    {
        const string email = "setpwddb@example.com";
        var (_, accessToken) = await SeedExternalUserAsync(email);
        var auth = fixture.CreateAuthenticatedClientWithToken(accessToken);

        await auth.PostAsJsonAsync("/v1/users/me/password/initial",
            new { password = "NewPassword1!", googleIdToken = "fake-google-token" });

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        var emailVal = Email.Create(email).Value;
        var user = await db.Users.FirstAsync(u => u.Email == emailVal);
        Assert.NotNull(user.PasswordHash);
    }

    [Fact]
    public async Task SetInitialPassword_AfterSetting_CanLoginWithPassword()
    {
        const string email = "setpwdlogin@example.com";
        var (_, accessToken) = await SeedExternalUserAsync(email);
        var auth = fixture.CreateAuthenticatedClientWithToken(accessToken);

        await auth.PostAsJsonAsync("/v1/users/me/password/initial",
            new { password = "NewPassword1!", googleIdToken = "fake-google-token" });

        var loginResp = await _anon.PostAsJsonAsync("/v1/users/login",
            new { email, password = "NewPassword1!" });

        Assert.Equal(HttpStatusCode.OK, loginResp.StatusCode);
    }

    [Fact]
    public async Task SetInitialPassword_WhenUserAlreadyHasPassword_Returns409()
    {
        const string email = "haspwd@example.com";
        var (_, accessToken) = await SeedExternalUserAsync(email);
        var auth = fixture.CreateAuthenticatedClientWithToken(accessToken);

        // First call succeeds and sets the password.
        await auth.PostAsJsonAsync("/v1/users/me/password/initial",
            new { password = "NewPassword1!", googleIdToken = "fake-google-token" });

        // Second call must be rejected — password is already set.
        var response = await auth.PostAsJsonAsync("/v1/users/me/password/initial",
            new { password = "AnotherPassword1!", googleIdToken = "fake-google-token" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task SetInitialPassword_WithShortPassword_Returns422()
    {
        var (_, accessToken) = await SeedExternalUserAsync("setpwdshort@example.com");
        var auth = fixture.CreateAuthenticatedClientWithToken(accessToken);

        var response = await auth.PostAsJsonAsync("/v1/users/me/password/initial",
            new { password = "short", googleIdToken = "fake-google-token" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task SetInitialPassword_WithPasswordExceedingMaxLength_Returns422()
    {
        var (_, accessToken) = await SeedExternalUserAsync("setpwdlong@example.com");
        var auth = fixture.CreateAuthenticatedClientWithToken(accessToken);
        var oversized = new string('A', 129) + "1!";

        var response = await auth.PostAsJsonAsync("/v1/users/me/password/initial",
            new { password = oversized, googleIdToken = "fake-google-token" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task SetInitialPassword_WhenGoogleIdTokenExceedsMaxLength_Returns422()
    {
        var (_, accessToken) = await SeedExternalUserAsync("setpwdtokenlong@example.com");
        var auth = fixture.CreateAuthenticatedClientWithToken(accessToken);
        var oversized = new string('a', 4097);

        var response = await auth.PostAsJsonAsync("/v1/users/me/password/initial",
            new { password = "NewPassword1!", googleIdToken = oversized });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task SetInitialPassword_WhenUnauthenticated_Returns401()
    {
        var response = await _anon.PostAsJsonAsync("/v1/users/me/password/initial",
            new { password = "NewPassword1!", googleIdToken = "fake-google-token" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SetInitialPassword_WithInvalidGoogleToken_Returns401()
    {
        var (_, accessToken) = await SeedExternalUserAsync("setpwdinvalidtoken@example.com");
        fixture.GoogleVerifier.SetError(Error.Unauthorized("Google.InvalidToken", "Token verification failed."));
        var auth = fixture.CreateAuthenticatedClientWithToken(accessToken);

        var response = await auth.PostAsJsonAsync("/v1/users/me/password/initial",
            new { password = "NewPassword1!", googleIdToken = "bad-token" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SetInitialPassword_WithMismatchedGoogleSubject_Returns401()
    {
        var (_, accessToken) = await SeedExternalUserAsync("setpwdmismatch@example.com");
        // Verifier returns a different subject than the one linked to this user.
        fixture.GoogleVerifier.SetIdentity("some-other-google-sub", "other@example.com");
        var auth = fixture.CreateAuthenticatedClientWithToken(accessToken);

        var response = await auth.PostAsJsonAsync("/v1/users/me/password/initial",
            new { password = "NewPassword1!", googleIdToken = "fake-google-token" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SetInitialPassword_RevokesAllRefreshTokens_IncludingCurrentSession()
    {
        const string email = "setpwdrevoke@example.com";
        const string subject = "sub-setpwd-revoke";

        // Session 1 — confirm flow creates the user and issues real tokens
        var rawToken1 = await SeedPendingLoginAsync(subject, email, isExistingUser: false);
        var confirm1Resp = await _anon.PostAsJsonAsync("/v1/users/auth/google/confirm",
            new GoogleLoginConfirmRequest(rawToken1));
        var session1 = await confirm1Resp.Content.ReadFromJsonAsync<GoogleLoginConfirmResponse>();
        Assert.NotNull(session1);

        // Session 2 — second "device" confirms another pending login for the same account.
        // Must use a different Google subject so LinkToExistingUserAsync doesn't collide
        // with the Google link already created by session 1's ProvisionNewUserAsync.
        var rawToken2 = await SeedPendingLoginAsync("sub-setpwd-revoke-2", email, isExistingUser: true);
        var confirm2Resp = await _anon.PostAsJsonAsync("/v1/users/auth/google/confirm",
            new GoogleLoginConfirmRequest(rawToken2));
        var session2 = await confirm2Resp.Content.ReadFromJsonAsync<GoogleLoginConfirmResponse>();
        Assert.NotNull(session2);

        // Step-up: present the Google ID token for the subject linked during account provisioning.
        fixture.GoogleVerifier.SetIdentity(subject, email);
        var auth = fixture.CreateAuthenticatedClientWithToken(session1.AccessToken);
        await auth.PostAsJsonAsync("/v1/users/me/password/initial",
            new { password = "NewPassword1!", googleIdToken = "fake-google-token" });

        // session1 refresh token (the requesting session) must also be revoked — no stolen-token persistence.
        var rt1Refresh = await _anon.PostAsJsonAsync("/v1/users/token/refresh",
            new RefreshTokenRequest(session1.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, rt1Refresh.StatusCode);

        // session2 refresh token (other session) must be revoked.
        var rt2Refresh = await _anon.PostAsJsonAsync("/v1/users/token/refresh",
            new RefreshTokenRequest(session2.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, rt2Refresh.StatusCode);
    }

    private async Task<(Guid UserId, string AccessToken)> SeedExternalUserAsync(string email)
    {
        const string subject = "sub-external-setpwd";
        Guid userId;
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            var emailVal = Email.Create(email).Value;
            var user = User.CreateExternal(emailVal, "ExternalUser", ExternalLoginProvider.Google, subject, clock).Value;
            user.LinkExternalLogin(ExternalLoginProvider.Google, subject, clock.UtcNow);
            db.Users.Add(user);
            await db.SaveChangesAsync();
            userId = user.Id.Value;
        }

        // Configure the fake verifier to match the seeded Google subject.
        fixture.GoogleVerifier.SetIdentity(subject, email);

        var accessToken = ApiTestFixture.GenerateTestToken(userId, email, "ExternalUser");
        return (userId, accessToken);
    }

    private async Task<string> SeedPendingLoginAsync(string subject, string email, bool isExistingUser)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var (pending, rawToken) = PendingExternalLogin.Create(
            ExternalLoginProvider.Google, subject, email, "Test User",
            isExistingUser, createdFromIp: null, userAgent: null,
            TimeSpan.FromMinutes(15), clock);

        db.PendingExternalLogins.Add(pending);
        await db.SaveChangesAsync();
        return rawToken;
    }
}
