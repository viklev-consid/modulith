using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Modules.Organizations.Domain;
using Modulith.Modules.Organizations.Persistence;
using Modulith.Modules.Users.Features.Register;

namespace Modulith.Modules.Organizations.IntegrationTests.Features;

[Collection("InviteOnlyOrganizationsModule")]
[Trait("Category", "Integration")]
public sealed class OrganizationInviteOnlyRegistrationTests(InviteOnlyOrganizationsApiFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Register_WithOrganizationInvite_WhenUsersInviteOnly_CreatesUserAndMembership()
    {
        var ownerId = Guid.NewGuid();
        var (organizationId, rawToken) = await CreateOrganizationInvitationAsync(ownerId);
        using var client = fixture.CreateAnonymousClient();

        var response = await client.PostAsJsonAsync(
            "/v1/users/register",
            new RegisterRequest("invite-only@example.com", "Password1!", "Invite Only", OrganizationInvitationToken: rawToken));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var members = await fixture.QueryDbAsync<OrganizationsDbContext, int>((db, ct) =>
            db.Memberships.CountAsync(m => m.OrganizationId == organizationId && m.IsActive, ct));
        Assert.Equal(2, members);
    }

    private async Task<(OrganizationId OrganizationId, string RawToken)> CreateOrganizationInvitationAsync(Guid ownerId)
    {
        string? rawToken = null;
        OrganizationId? organizationId = null;

        await fixture.ExecuteDbAsync<OrganizationsDbContext>(async (db, ct) =>
        {
            var clock = fixture.Services.GetRequiredService<Modulith.Shared.Kernel.Interfaces.IClock>();
            var organization = Organization.Create(
                "Invite Only Org",
                OrganizationSlug.Create("invite-only-org").Value,
                ownerId,
                clock).Value;
            db.Organizations.Add(organization);
            var invitation = OrganizationInvitation.Create(
                organization.Id,
                "invite-only@example.com",
                OrganizationRole.Member,
                TimeSpan.FromDays(7),
                ownerId,
                clock).Value;
            db.Invitations.Add(invitation.Invitation);
            await db.SaveChangesAsync(ct);
            organizationId = organization.Id;
            rawToken = invitation.RawToken;
        });

        return (organizationId!, rawToken!);
    }
}
