using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Features.Login;
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
}
