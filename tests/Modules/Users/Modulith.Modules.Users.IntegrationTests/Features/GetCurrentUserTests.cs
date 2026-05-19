using System.Net;
using System.Net.Http.Json;
using Modulith.Modules.Users.Features.GetCurrentUser;
using Modulith.Modules.Users.Features.Register;
using Modulith.Modules.Users.Features.TwoFactor.ConfirmTotp;
using Modulith.Modules.Users.Features.TwoFactor.SetupTotp;
using Modulith.Modules.Users.IntegrationTests.Support;

namespace Modulith.Modules.Users.IntegrationTests.Features;

[Collection("UsersModule")]
[Trait("Category", "Integration")]
public sealed class GetCurrentUserTests(UsersApiFixture fixture) : IAsyncLifetime
{
    private readonly HttpClient anonymous = fixture.CreateAnonymousClient();

    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetCurrentUser_Authenticated_ReturnsProfile()
    {
        var registerResponse = await (await anonymous.PostAsJsonAsync("/v1/users/register",
            new RegisterRequest("alice@example.com", "Password1!", "Alice")))
            .Content.ReadFromJsonAsync<RegisterResponse>();

        var client = fixture.CreateAuthenticatedClient(
            registerResponse!.UserId, "alice@example.com", "Alice");

        var response = await client.GetAsync("/v1/users/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<GetCurrentUserResponse>();
        Assert.NotNull(body);
        Assert.Equal(registerResponse.UserId, body.UserId);
        Assert.Equal("alice@example.com", body.Email);
        Assert.Equal("Alice", body.DisplayName);
        Assert.False(body.TwoFactorEnabled);
    }

    [Fact]
    public async Task GetCurrentUser_WhenTwoFactorEnabled_ReturnsTwoFactorEnabled()
    {
        var registerResponse = await (await anonymous.PostAsJsonAsync("/v1/users/register",
            new RegisterRequest("totp-me@example.com", "Password1!", "Totp User")))
            .Content.ReadFromJsonAsync<RegisterResponse>();

        var client = fixture.CreateAuthenticatedClient(
            registerResponse!.UserId, "totp-me@example.com", "Totp User");

        var setupResponse = await client.PostAsync("/v1/users/me/2fa/totp/setup", content: null);
        setupResponse.EnsureSuccessStatusCode();
        var setup = (await setupResponse.Content.ReadFromJsonAsync<SetupTotpResponse>())!;

        var confirmResponse = await client.PostAsJsonAsync(
            "/v1/users/me/2fa/totp/confirm",
            new ConfirmTotpRequest(TotpTestHelper.Current(setup.Secret, fixture.Clock.UtcNow)));
        confirmResponse.EnsureSuccessStatusCode();

        var response = await client.GetAsync("/v1/users/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<GetCurrentUserResponse>();
        Assert.NotNull(body);
        Assert.True(body.TwoFactorEnabled);
    }

    [Fact]
    public async Task GetCurrentUser_Unauthenticated_Returns401()
    {
        var response = await anonymous.GetAsync("/v1/users/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
