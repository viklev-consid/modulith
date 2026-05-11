using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Features.Register;
using Modulith.Modules.Users.Persistence;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Users.IntegrationTests.Features;

[Collection("InviteOnlyUsersModule")]
[Trait("Category", "Integration")]
public sealed class InviteOnlyRegisterTests(InviteOnlyUsersApiFixture fixture) : IAsyncLifetime
{
    private readonly HttpClient client = fixture.CreateAnonymousClient();

    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Register_WithoutInvitation_Returns422()
    {
        var response = await client.PostAsJsonAsync("/v1/users/register",
            new RegisterRequest("alice@example.com", "Password1!", "Alice"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithValidInvitation_Returns201AndConsumesInvitation()
    {
        var (invitationId, token) = await SeedInvitationAsync("alice@example.com");

        var response = await client.PostAsJsonAsync("/v1/users/register",
            new RegisterRequest("ALICE@example.com", "Password1!", "Alice", token));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RegisterResponse>();
        Assert.NotNull(body);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        var invitation = await db.UserInvitations.FirstAsync(i => i.Id == invitationId);
        Assert.NotNull(invitation.AcceptedAt);
        Assert.Equal(new UserId(body.UserId), invitation.AcceptedUserId);
    }

    [Fact]
    public async Task Register_WithInvitationForDifferentEmail_Returns422()
    {
        var (_, token) = await SeedInvitationAsync("alice@example.com");

        var response = await client.PostAsJsonAsync("/v1/users/register",
            new RegisterRequest("bob@example.com", "Password1!", "Bob", token));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_ReusingInvitation_Returns422()
    {
        var (_, token) = await SeedInvitationAsync("alice@example.com");
        await client.PostAsJsonAsync("/v1/users/register",
            new RegisterRequest("alice@example.com", "Password1!", "Alice", token));

        var response = await client.PostAsJsonAsync("/v1/users/register",
            new RegisterRequest("alice2@example.com", "Password1!", "Alice", token));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<(UserInvitationId InvitationId, string Token)> SeedInvitationAsync(string email, TimeSpan? lifetime = null)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var invitationResult = UserInvitation.Create(Email.Create(email).Value, lifetime ?? TimeSpan.FromDays(7), clock);
        var (invitation, token) = invitationResult.Value;
        db.UserInvitations.Add(invitation);
        await db.SaveChangesAsync();
        return (invitation.Id, token);
    }
}

[Collection("RegistrationDisabledUsersModule")]
[Trait("Category", "Integration")]
public sealed class RegistrationDisabledTests(RegistrationDisabledUsersApiFixture fixture) : IAsyncLifetime
{
    private readonly HttpClient client = fixture.CreateAnonymousClient();

    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Register_WhenRegistrationDisabled_Returns422()
    {
        var response = await client.PostAsJsonAsync("/v1/users/register",
            new RegisterRequest("alice@example.com", "Password1!", "Alice"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
