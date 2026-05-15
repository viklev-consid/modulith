using System.Net;
using System.Net.Http.Json;
using Modulith.Modules.Users.Features.GetCurrentUser;
using Modulith.Modules.Users.Features.Register;
using Modulith.Modules.Users.Features.UpdateProfile;

namespace Modulith.Modules.Users.IntegrationTests.Features;

[Collection("UsersModule")]
[Trait("Category", "Integration")]
public sealed class UpdateProfileTests(UsersApiFixture fixture) : IAsyncLifetime
{
    private readonly HttpClient anonymous = fixture.CreateAnonymousClient();

    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task UpdateProfile_Authenticated_UpdatesDisplayName()
    {
        var registered = await RegisterAsync("profile@example.com", "Original Name");
        var client = fixture.CreateAuthenticatedClient(
            registered.UserId,
            "profile@example.com",
            "Original Name");

        var response = await client.PatchAsJsonAsync(
            "/v1/users/me/profile",
            new UpdateProfileRequest("  Updated Name  "));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<UpdateProfileResponse>();
        Assert.NotNull(body);
        Assert.Equal(registered.UserId, body.UserId);
        Assert.Equal("profile@example.com", body.Email);
        Assert.Equal("Updated Name", body.DisplayName);

        var me = await client.GetAsync("/v1/users/me");
        var current = await me.Content.ReadFromJsonAsync<GetCurrentUserResponse>();
        Assert.NotNull(current);
        Assert.Equal("Updated Name", current.DisplayName);
    }

    [Fact]
    public async Task UpdateProfile_WithEmptyDisplayName_Returns422()
    {
        var registered = await RegisterAsync("empty-profile@example.com", "Original Name");
        var client = fixture.CreateAuthenticatedClient(
            registered.UserId,
            "empty-profile@example.com",
            "Original Name");

        var response = await client.PatchAsJsonAsync(
            "/v1/users/me/profile",
            new UpdateProfileRequest(""));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProfile_Unauthenticated_Returns401()
    {
        var response = await anonymous.PatchAsJsonAsync(
            "/v1/users/me/profile",
            new UpdateProfileRequest("Updated Name"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<RegisterResponse> RegisterAsync(string email, string displayName)
    {
        var response = await anonymous.PostAsJsonAsync(
            "/v1/users/register",
            new RegisterRequest(email, "Password1!", displayName));
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<RegisterResponse>())!;
    }
}
