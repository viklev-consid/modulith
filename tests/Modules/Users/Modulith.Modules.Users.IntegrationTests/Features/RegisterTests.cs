using System.Net;
using System.Net.Http.Json;
using Modulith.Modules.Users.Features.Register;

namespace Modulith.Modules.Users.IntegrationTests.Features;

[Collection("UsersModule")]
[Trait("Category", "Integration")]
public sealed class RegisterTests(UsersApiFixture fixture) : IAsyncLifetime
{
    private readonly HttpClient _client = fixture.CreateAnonymousClient();

    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Register_WithValidRequest_Returns201AndToken()
    {
        var request = new RegisterRequest("alice@example.com", "Password1!", "Alice");

        var response = await _client.PostAsJsonAsync("/v1/users/register", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RegisterResponse>();
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body.UserId);
        Assert.NotEmpty(body.AccessToken);
        Assert.NotEmpty(body.RefreshToken);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_Returns409()
    {
        var request = new RegisterRequest("alice@example.com", "Password1!", "Alice");
        await _client.PostAsJsonAsync("/v1/users/register", request);

        var response = await _client.PostAsJsonAsync("/v1/users/register", request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithInvalidEmail_Returns422()
    {
        var request = new RegisterRequest("not-an-email", "Password1!", "Alice");

        var response = await _client.PostAsJsonAsync("/v1/users/register", request);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithShortPassword_Returns422()
    {
        var request = new RegisterRequest("alice@example.com", "short", "Alice");

        var response = await _client.PostAsJsonAsync("/v1/users/register", request);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Register_EmailIsCaseInsensitive()
    {
        await _client.PostAsJsonAsync("/v1/users/register",
            new RegisterRequest("Alice@Example.COM", "Password1!", "Alice"));

        var response = await _client.PostAsJsonAsync("/v1/users/register",
            new RegisterRequest("alice@example.com", "Password1!", "Alice"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
