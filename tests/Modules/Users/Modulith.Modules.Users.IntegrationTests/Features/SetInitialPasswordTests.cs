using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Features.ExternalLogin.Google.Confirm;
using Modulith.Modules.Users.Features.Login;
using Modulith.Modules.Users.Features.RefreshToken;
using Modulith.Modules.Users.Features.Register;
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
            new { password = "NewPassword1!" });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task SetInitialPassword_PersistsPasswordHashInDatabase()
    {
        const string email = "setpwddb@example.com";
        var (_, accessToken) = await SeedExternalUserAsync(email);
        var auth = fixture.CreateAuthenticatedClientWithToken(accessToken);

        await auth.PostAsJsonAsync("/v1/users/me/password/initial",
            new { password = "NewPassword1!" });

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
            new { password = "NewPassword1!" });

        var loginResp = await _anon.PostAsJsonAsync("/v1/users/login",
            new LoginRequest(email, "NewPassword1!"));

        Assert.Equal(HttpStatusCode.OK, loginResp.StatusCode);
    }

    [Fact]
    public async Task SetInitialPassword_WhenUserAlreadyHasPassword_Returns409()
    {
        var reg = await _anon.PostAsJsonAsync("/v1/users/register",
            new RegisterRequest("haspwd@example.com", "Password1!", "Alice"));
        var regBody = await reg.Content.ReadFromJsonAsync<RegisterResponse>();
        Assert.NotNull(regBody);

        var login = await _anon.PostAsJsonAsync("/v1/users/login",
            new LoginRequest("haspwd@example.com", "Password1!"));
        var loginBody = await login.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(loginBody);

        var auth = fixture.CreateAuthenticatedClientWithToken(loginBody.AccessToken);

        var response = await auth.PostAsJsonAsync("/v1/users/me/password/initial",
            new { password = "NewPassword1!" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task SetInitialPassword_WithShortPassword_Returns422()
    {
        var (_, accessToken) = await SeedExternalUserAsync("setpwdshort@example.com");
        var auth = fixture.CreateAuthenticatedClientWithToken(accessToken);

        var response = await auth.PostAsJsonAsync("/v1/users/me/password/initial",
            new { password = "short" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task SetInitialPassword_WhenUnauthenticated_Returns401()
    {
        var response = await _anon.PostAsJsonAsync("/v1/users/me/password/initial",
            new { password = "NewPassword1!" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SetInitialPassword_RevokesOtherRefreshTokens_PreservesCurrentSession()
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

        // Set initial password using session1's access token (carries rtid in claims)
        var auth = fixture.CreateAuthenticatedClientWithToken(session1.AccessToken);
        await auth.PostAsJsonAsync("/v1/users/me/password/initial",
            new { password = "NewPassword1!" });

        // session1 refresh token (the current session) should still work
        var rt1Refresh = await _anon.PostAsJsonAsync("/v1/users/token/refresh",
            new RefreshTokenRequest(session1.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, rt1Refresh.StatusCode);

        // session2 refresh token (other session) should be revoked
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
            db.Users.Add(user);
            await db.SaveChangesAsync();
            userId = user.Id.Value;
        }

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
