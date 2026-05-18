using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Features.GetCurrentUser;
using Modulith.Modules.Users.Features.Register;
using Modulith.Modules.Users.Persistence;
using Modulith.Modules.Users.Features.TwoFactor.ConfirmTotp;
using Modulith.Modules.Users.Features.TwoFactor.SetupTotp;
using Modulith.Modules.Users.IntegrationTests.Support;
using Modulith.Shared.Kernel.Interfaces;

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
    public async Task GetCurrentUser_WhenExternalLoginLinked_ReturnsLinkedAccountProviderEmail()
    {
        var registerResponse = await (await anonymous.PostAsJsonAsync("/v1/users/register",
            new RegisterRequest("local@example.com", "Password1!", "Local User")))
            .Content.ReadFromJsonAsync<RegisterResponse>();

        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            var userId = new UserId(registerResponse!.UserId);
            var user = await db.Users
                .Include(u => u.ExternalLogins)
                .FirstAsync(u => u.Id == userId);
            var linkResult = user.LinkExternalLogin(
                ExternalLoginProvider.Google,
                "sub-current-user",
                "linked-google@example.com",
                clock.UtcNow);
            Assert.False(linkResult.IsError);
            await db.SaveChangesAsync();
        }

        var client = fixture.CreateAuthenticatedClient(
            registerResponse!.UserId, "local@example.com", "Local User");

        var response = await client.GetAsync("/v1/users/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<GetCurrentUserResponse>();
        Assert.NotNull(body);
        var linkedAccount = Assert.Single(body.LinkedAccounts);
        Assert.Equal("Google", linkedAccount.Provider);
        Assert.Equal("linked-google@example.com", linkedAccount.ProviderEmail);
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
            new ConfirmTotpRequest(TotpTestHelper.Current(setup.Secret)));
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
