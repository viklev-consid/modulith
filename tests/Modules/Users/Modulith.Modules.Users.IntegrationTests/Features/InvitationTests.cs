using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Features.CreateInvitation;
using Modulith.Modules.Users.Features.Register;
using Modulith.Modules.Users.Features.RevokeInvitation;
using Modulith.Modules.Users.Persistence;

namespace Modulith.Modules.Users.IntegrationTests.Features;

[Collection("UsersModule")]
[Trait("Category", "Integration")]
public sealed class InvitationTests(UsersApiFixture fixture) : IAsyncLifetime
{
    private readonly HttpClient anon = fixture.CreateAnonymousClient();

    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CreateInvitation_Admin_Returns201AndPublishesToken()
    {
        var admin = await RegisterAsync("admin@example.com", "Admin");

        var response = await AdminClient(admin.UserId, "admin@example.com")
            .PostAsJsonAsync("/v1/users/invitations", new CreateInvitationRequest("invitee@example.com"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CreateInvitationResponse>();
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body.InvitationId);
        Assert.Equal("invitee@example.com", body.Email);
        Assert.NotEmpty(body.Token);
    }

    [Fact]
    public async Task CreateInvitation_RegularUser_Returns403()
    {
        var user = await RegisterAsync("user@example.com");

        var response = await UserClient(user.UserId, "user@example.com")
            .PostAsJsonAsync("/v1/users/invitations", new CreateInvitationRequest("invitee@example.com"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateInvitation_WhenActiveInvitationExists_Returns409()
    {
        var admin = await RegisterAsync("admin@example.com", "Admin");
        var client = AdminClient(admin.UserId, "admin@example.com");
        await client.PostAsJsonAsync("/v1/users/invitations", new CreateInvitationRequest("invitee@example.com"));

        var response = await client.PostAsJsonAsync("/v1/users/invitations", new CreateInvitationRequest("INVITEE@example.com"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task RevokeInvitation_Admin_Returns200AndMarksInvitationRevoked()
    {
        var admin = await RegisterAsync("admin@example.com", "Admin");
        var client = AdminClient(admin.UserId, "admin@example.com");
        var create = await client.PostAsJsonAsync("/v1/users/invitations", new CreateInvitationRequest("invitee@example.com"));
        var created = (await create.Content.ReadFromJsonAsync<CreateInvitationResponse>())!;

        var response = await client.DeleteAsync($"/v1/users/invitations/{created.InvitationId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RevokeInvitationResponse>();
        Assert.NotNull(body);
        Assert.Equal(created.InvitationId, body.InvitationId);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        var invitation = await db.UserInvitations.FirstAsync(i => i.Id == new UserInvitationId(created.InvitationId));
        Assert.NotNull(invitation.RevokedAt);
    }

    private async Task<RegisterResponse> RegisterAsync(string email, string name = "Test User")
    {
        var resp = await anon.PostAsJsonAsync("/v1/users/register",
            new RegisterRequest(email, "Password1!", name));
        return (await resp.Content.ReadFromJsonAsync<RegisterResponse>())!;
    }

    private HttpClient AdminClient(Guid userId, string email)
        => fixture.CreateAuthenticatedClient(userId, email, "Admin", "admin");

    private HttpClient UserClient(Guid userId, string email)
        => fixture.CreateAuthenticatedClient(userId, email, "User", "user");
}
