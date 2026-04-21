using System.Net;
using System.Net.Http.Json;
using Modulith.Modules.Users.Features.ChangePassword;
using Modulith.Modules.Users.Features.Login;
using Modulith.Modules.Users.Features.RefreshToken;
using Modulith.Modules.Users.Features.Register;

namespace Modulith.Modules.Users.IntegrationTests.Features;

[Collection("UsersModule")]
[Trait("Category", "Integration")]
public sealed class ChangePasswordTests(UsersApiFixture fixture) : IAsyncLifetime
{
    private readonly HttpClient _anon = fixture.CreateAnonymousClient();

    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ChangePassword_WithCorrectCurrentPassword_Returns200()
    {
        var login = await RegisterAndLoginAsync("alice@example.com");
        var auth = fixture.CreateAuthenticatedClientWithToken(login.AccessToken);

        var response = await auth.PostAsJsonAsync("/v1/users/me/password",
            new ChangePasswordRequest("Password1!", "NewPassword1!"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_WithWrongCurrentPassword_Returns401()
    {
        var login = await RegisterAndLoginAsync("alice@example.com");
        var auth = fixture.CreateAuthenticatedClientWithToken(login.AccessToken);

        var response = await auth.PostAsJsonAsync("/v1/users/me/password",
            new ChangePasswordRequest("WrongPassword1!", "NewPassword1!"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_RevokesOtherRefreshTokens_PreservesCurrentSession()
    {
        var login1 = await RegisterAndLoginAsync("alice@example.com");

        // Login from a second "device"
        var login2Resp = await _anon.PostAsJsonAsync("/v1/users/login",
            new LoginRequest("alice@example.com", "Password1!"));
        var login2 = await login2Resp.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(login2);

        // Change password with the first session's access token (includes rtid)
        var auth = fixture.CreateAuthenticatedClientWithToken(login1.AccessToken);
        await auth.PostAsJsonAsync("/v1/users/me/password",
            new ChangePasswordRequest("Password1!", "NewPassword1!"));

        // login1 refresh token (the current session) should still work
        var rt1Refresh = await _anon.PostAsJsonAsync("/v1/users/token/refresh",
            new RefreshTokenRequest(login1.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, rt1Refresh.StatusCode);

        // login2 refresh token (other session) should be revoked
        var rt2Refresh = await _anon.PostAsJsonAsync("/v1/users/token/refresh",
            new RefreshTokenRequest(login2.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, rt2Refresh.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_RequiresAuthentication()
    {
        var response = await _anon.PostAsJsonAsync("/v1/users/me/password",
            new ChangePasswordRequest("Password1!", "NewPassword1!"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<LoginResponse> RegisterAndLoginAsync(string email)
    {
        await _anon.PostAsJsonAsync("/v1/users/register",
            new RegisterRequest(email, "Password1!", "Alice"));

        var response = await _anon.PostAsJsonAsync("/v1/users/login",
            new LoginRequest(email, "Password1!"));

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(body);
        return body;
    }
}
