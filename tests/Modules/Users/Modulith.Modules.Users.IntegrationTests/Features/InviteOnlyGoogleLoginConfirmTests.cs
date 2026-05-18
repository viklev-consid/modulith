using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Features.ExternalLogin.Google.Confirm;
using Modulith.Modules.Users.Features.Register;
using Modulith.Modules.Users.Persistence;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Users.IntegrationTests.Features;

[Collection("InviteOnlyGoogleUsersModule")]
[Trait("Category", "Integration")]
public sealed class InviteOnlyGoogleLoginConfirmTests(InviteOnlyGoogleUsersApiFixture fixture) : IAsyncLifetime
{
    private readonly HttpClient client = fixture.CreateAnonymousClient();

    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GoogleLoginConfirm_ForNewUserWithoutInvitation_Returns422()
    {
        var pendingToken = await SeedPendingLoginAsync("sub-new", "newgoogle@example.com", isExistingUser: false);

        var response = await client.PostAsJsonAsync("/v1/users/auth/google/confirm",
            new GoogleLoginConfirmRequest(pendingToken));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GoogleLoginConfirm_ForNewUserWithInvitation_Returns200AndConsumesInvitation()
    {
        const string email = "newgoogle@example.com";
        var pendingToken = await SeedPendingLoginAsync("sub-new", email, isExistingUser: false);
        var (invitationId, invitationToken) = await SeedInvitationAsync(email);

        var response = await client.PostAsJsonAsync("/v1/users/auth/google/confirm",
            new GoogleLoginConfirmRequest(pendingToken, invitationToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<GoogleLoginConfirmResponse>();
        Assert.NotNull(body);
        Assert.True(body.IsNewUser);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        var invitation = await db.UserInvitations.FirstAsync(i => i.Id == invitationId);
        Assert.NotNull(invitation.AcceptedAt);
        Assert.Equal(new UserId(body.UserId), invitation.AcceptedUserId);
    }

    [Fact]
    public async Task GoogleLoginConfirm_ForExistingUser_DoesNotRequireInvitation()
    {
        const string email = "existing@example.com";
        await SeedUserAsync(email);
        var pendingToken = await SeedPendingLoginAsync("sub-existing", email, isExistingUser: true);

        var response = await client.PostAsJsonAsync("/v1/users/auth/google/confirm",
            new GoogleLoginConfirmRequest(pendingToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<GoogleLoginConfirmResponse>();
        Assert.NotNull(body);
        Assert.False(body.IsNewUser);
    }

    private async Task SeedUserAsync(string email)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        var user = User.CreateWithPassword(Email.Create(email).Value, new PasswordHash("hashed-password"), "Existing").Value;
        db.Users.Add(user);
        await db.SaveChangesAsync();
    }

    private async Task<string> SeedPendingLoginAsync(string subject, string email, bool isExistingUser)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var (pending, rawToken) = PendingExternalLogin.Create(
            ExternalLoginProvider.Google,
            subject,
            email,
            "Test User",
            null, isExistingUser,
            createdFromIp: null,
            userAgent: null,
            TimeSpan.FromMinutes(15),
            clock);

        db.PendingExternalLogins.Add(pending);
        await db.SaveChangesAsync();
        return rawToken;
    }

    private async Task<(UserInvitationId InvitationId, string Token)> SeedInvitationAsync(string email)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var (invitation, token) = UserInvitation.Create(Email.Create(email).Value, TimeSpan.FromDays(7), clock).Value;
        db.UserInvitations.Add(invitation);
        await db.SaveChangesAsync();
        return (invitation.Id, token);
    }
}
