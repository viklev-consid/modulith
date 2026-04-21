using System.Net;
using System.Net.Http.Json;
using Modulith.Modules.Users.Features.Login;
using Modulith.Modules.Users.Features.Logout;
using Modulith.Modules.Users.Features.RefreshToken;
using Modulith.Modules.Users.Features.Register;

namespace Modulith.Modules.Users.IntegrationTests.Features;

[Collection("UsersModule")]
[Trait("Category", "Integration")]
public sealed class LogoutTests(UsersApiFixture fixture) : IAsyncLifetime
{
    private readonly HttpClient _anon = fixture.CreateAnonymousClient();

    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Logout_RevokesSpecificRefreshToken()
    {
        var login = await RegisterAndLoginAsync();
        var auth = fixture.CreateAuthenticatedClientWithToken(login.AccessToken);

        await auth.PostAsJsonAsync("/v1/users/logout", new LogoutRequest(login.RefreshToken));

        var refresh = await _anon.PostAsJsonAsync("/v1/users/token/refresh",
            new RefreshTokenRequest(login.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Logout_DoesNotAffectOtherSessions()
    {
        var login1 = await RegisterAndLoginAsync();

        var login2Resp = await _anon.PostAsJsonAsync("/v1/users/login",
            new LoginRequest("alice@example.com", "Password1!"));
        var login2 = await login2Resp.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(login2);

        var auth = fixture.CreateAuthenticatedClientWithToken(login1.AccessToken);
        await auth.PostAsJsonAsync("/v1/users/logout", new LogoutRequest(login1.RefreshToken));

        // Session 2 must still work
        var rt2Refresh = await _anon.PostAsJsonAsync("/v1/users/token/refresh",
            new RefreshTokenRequest(login2.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, rt2Refresh.StatusCode);
    }

    [Fact]
    public async Task LogoutAll_RevokesAllSessions()
    {
        var login1 = await RegisterAndLoginAsync();

        var login2Resp = await _anon.PostAsJsonAsync("/v1/users/login",
            new LoginRequest("alice@example.com", "Password1!"));
        var login2 = await login2Resp.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(login2);

        var auth = fixture.CreateAuthenticatedClientWithToken(login1.AccessToken);
        var response = await auth.PostAsync("/v1/users/logout/all", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Both sessions must be revoked
        var rt1 = await _anon.PostAsJsonAsync("/v1/users/token/refresh",
            new RefreshTokenRequest(login1.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, rt1.StatusCode);

        var rt2 = await _anon.PostAsJsonAsync("/v1/users/token/refresh",
            new RefreshTokenRequest(login2.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, rt2.StatusCode);
    }

    [Fact]
    public async Task Logout_RequiresAuthentication()
    {
        var response = await _anon.PostAsJsonAsync("/v1/users/logout",
            new LogoutRequest("some-token"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<LoginResponse> RegisterAndLoginAsync()
    {
        await _anon.PostAsJsonAsync("/v1/users/register",
            new RegisterRequest("alice@example.com", "Password1!", "Alice"));

        var response = await _anon.PostAsJsonAsync("/v1/users/login",
            new LoginRequest("alice@example.com", "Password1!"));

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(body);
        return body;
    }
}
