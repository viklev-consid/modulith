using System.Net;
using System.Net.Http.Json;
using Modulith.Modules.Users.Features.Register;

namespace Modulith.Modules.Users.IntegrationTests.Features;

[Collection("UsersGdpr")]
[Trait("Category", "Integration")]
public sealed class DeleteAccountTests(GdprApiFixture fixture) : IAsyncLifetime
{
    private readonly HttpClient _anonymous = fixture.CreateAnonymousClient();

    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task DeleteAccount_Authenticated_Returns204()
    {
        var registerResp = await (await _anonymous.PostAsJsonAsync("/v1/users/register",
            new RegisterRequest("alice@example.com", "Password1!", "Alice")))
            .Content.ReadFromJsonAsync<RegisterResponse>();

        var client = fixture.CreateAuthenticatedClient(
            registerResp!.UserId, "alice@example.com", "Alice");

        var response = await client.DeleteAsync("/v1/users/me");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteAccount_AfterDeletion_UserCannotLoginAgain()
    {
        var registerResp = await (await _anonymous.PostAsJsonAsync("/v1/users/register",
            new RegisterRequest("alice@example.com", "Password1!", "Alice")))
            .Content.ReadFromJsonAsync<RegisterResponse>();

        var client = fixture.CreateAuthenticatedClient(
            registerResp!.UserId, "alice@example.com", "Alice");

        await client.DeleteAsync("/v1/users/me");

        // After deletion the user no longer exists — GET /me returns 404.
        var getMeResponse = await client.GetAsync("/v1/users/me");
        Assert.Equal(HttpStatusCode.NotFound, getMeResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteAccount_Unauthenticated_Returns401()
    {
        var response = await _anonymous.DeleteAsync("/v1/users/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
