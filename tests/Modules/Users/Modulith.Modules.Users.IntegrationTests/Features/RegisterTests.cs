using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Features.Register;
using Modulith.Modules.Users.Persistence;

namespace Modulith.Modules.Users.IntegrationTests.Features;

[Collection("UsersModule")]
[Trait("Category", "Integration")]
public sealed class RegisterTests(UsersApiFixture fixture) : IAsyncLifetime
{
    private readonly HttpClient client = fixture.CreateAnonymousClient();

    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Register_WithValidRequest_Returns201AndRequiresEmailConfirmation()
    {
        var request = new RegisterRequest("alice@example.com", "Password1!", "Alice");

        var response = await client.PostAsJsonAsync("/v1/users/register", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RegisterResponse>();
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body.UserId);
        Assert.Contains("confirm", body.Message, StringComparison.OrdinalIgnoreCase);

        var state = await fixture.QueryDbAsync<UsersDbContext, (bool Confirmed, bool HasCompletedOnboarding, int ConfirmationTokens)>((db, ct) =>
            db.Users
                .Where(u => u.Id == new UserId(body.UserId))
                .Select(u => new ValueTuple<bool, bool, int>(
                    u.IsEmailConfirmed,
                    u.HasCompletedOnboarding,
                    db.SingleUseTokens.Count(t => t.UserId == u.Id && t.Purpose == TokenPurpose.EmailConfirmation)))
                .SingleAsync(ct));
        Assert.False(state.Confirmed);
        Assert.False(state.HasCompletedOnboarding);
        Assert.Equal(1, state.ConfirmationTokens);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_Returns409()
    {
        var request = new RegisterRequest("alice@example.com", "Password1!", "Alice");
        await client.PostAsJsonAsync("/v1/users/register", request);

        var response = await client.PostAsJsonAsync("/v1/users/register", request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithInvalidEmail_Returns422()
    {
        var request = new RegisterRequest("not-an-email", "Password1!", "Alice");

        var response = await client.PostAsJsonAsync("/v1/users/register", request);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithShortPassword_Returns422()
    {
        var request = new RegisterRequest("alice@example.com", "short", "Alice");

        var response = await client.PostAsJsonAsync("/v1/users/register", request);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Register_EmailIsCaseInsensitive()
    {
        await client.PostAsJsonAsync("/v1/users/register",
            new RegisterRequest("Alice@Example.COM", "Password1!", "Alice"));

        var response = await client.PostAsJsonAsync("/v1/users/register",
            new RegisterRequest("alice@example.com", "Password1!", "Alice"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
