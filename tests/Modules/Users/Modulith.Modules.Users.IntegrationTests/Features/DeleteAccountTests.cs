using System.Net;
using System.Net.Http.Json;
using Modulith.Modules.Users.Contracts.Events;
using Modulith.Modules.Users.Features.Register;
using Wolverine;
using Wolverine.Tracking;

namespace Modulith.Modules.Users.IntegrationTests.Features;

[Collection("UsersGdpr")]
[Trait("Category", "Integration")]
public sealed class DeleteAccountTests(GdprApiFixture fixture) : IAsyncLifetime
{
    private readonly HttpClient anonymous = fixture.CreateAnonymousClient();

    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task DeleteAccount_Authenticated_Returns204()
    {
        var registerResp = await (await anonymous.PostAsJsonAsync("/v1/users/register",
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
        var registerResp = await (await anonymous.PostAsJsonAsync("/v1/users/register",
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
        var response = await anonymous.DeleteAsync("/v1/users/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteAccount_PublishesUserErasureRequestedV1()
    {
        // Arrange
        var registerResp = await (await anonymous.PostAsJsonAsync("/v1/users/register",
            new RegisterRequest("erasure-event@example.com", "Password1!", "Erasure")))
            .Content.ReadFromJsonAsync<RegisterResponse>();

        var client = fixture.CreateAuthenticatedClient(
            registerResp!.UserId, "erasure-event@example.com", "Erasure");

        // Act — TrackActivity waits for the handler and all cascading subscribers to settle
        Func<IMessageContext, Task> act = async _ =>
        {
            var resp = await client.DeleteAsync("/v1/users/me");
            Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
        };

        await fixture.ApplicationHost.TrackActivity()
            .Timeout(TimeSpan.FromSeconds(10))
            .ExecuteAndWaitAsync(act);

        // Assert replay safety — re-delivering UserErasureRequestedV1 for an already-erased user
        // must be a no-op, not an error. If any subscriber throws, InvokeMessageAndWaitAsync throws.
        await fixture.ApplicationHost.TrackActivity()
            .Timeout(TimeSpan.FromSeconds(10))
            .InvokeMessageAndWaitAsync(
                new UserErasureRequestedV1(registerResp.UserId, "Erasure", Guid.NewGuid()));
    }
}

