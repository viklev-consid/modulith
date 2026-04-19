using System.Net;
using System.Net.Http.Json;
using Modulith.Modules.Users.Features.GetCurrentUser;
using Modulith.Modules.Users.Features.Register;

namespace Modulith.Modules.Users.IntegrationTests.Features;

[Collection("UsersModule")]
[Trait("Category", "Integration")]
public sealed class GetCurrentUserTests(UsersApiFixture fixture) : IAsyncLifetime
{
    private readonly HttpClient _anonymous = fixture.CreateAnonymousClient();

    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetCurrentUser_Authenticated_ReturnsProfile()
    {
        var registerResponse = await (await _anonymous.PostAsJsonAsync("/v1/users/register",
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
    }

    [Fact]
    public async Task GetCurrentUser_Unauthenticated_Returns401()
    {
        var response = await _anonymous.GetAsync("/v1/users/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
