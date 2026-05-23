using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Modules.Audit.Persistence;
using Modulith.Modules.Organizations.Domain;
using Modulith.Modules.Organizations.Features.ChangeOrganizationMemberRole;
using Modulith.Modules.Organizations.Features.CreateOrganization;
using Modulith.Modules.Organizations.Features.CreateOrganizationInvitation;
using Modulith.Modules.Organizations.Features.GetOrganization;
using Modulith.Modules.Organizations.Features.ListMyOrganizations;
using Modulith.Modules.Organizations.Features.ListOrganizationMembers;
using Modulith.Modules.Organizations.Persistence;
using Modulith.Modules.Users.Features.Register;
using Wolverine;
using Wolverine.Tracking;

namespace Modulith.Modules.Organizations.IntegrationTests.Features;

[Collection("OrganizationsModule")]
[Trait("Category", "Integration")]
public sealed class OrganizationSecurityTests(OrganizationsApiFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task MemberOfAnotherOrganization_CannotReadMembers()
    {
        var ownerA = Guid.NewGuid();
        var ownerB = Guid.NewGuid();
        var orgA = await CreateOrganizationAsync(ownerA, "Org A", "org-a");
        await CreateOrganizationAsync(ownerB, "Org B", "org-b");
        using var client = fixture.CreateAuthenticatedClient(ownerA, "a@example.com", "A");

        var response = await client.GetAsync($"/v1/organizations/org-b/members");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var ownResponse = await client.GetAsync($"/v1/organizations/{orgA.Slug}/members");
        Assert.Equal(HttpStatusCode.OK, ownResponse.StatusCode);
    }

    [Fact]
    public async Task Admin_CannotPromoteSelfToOwner()
    {
        var ownerId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var org = await CreateOrganizationAsync(ownerId, "Acme", "acme");
        await AddMemberAsync(org.Id, adminId, OrganizationRole.Admin);
        using var adminClient = fixture.CreateAuthenticatedClient(adminId, "admin@example.com", "Admin");

        var response = await adminClient.PutAsJsonAsync(
            $"/v1/organizations/{org.Slug}/members/{adminId}/role",
            new ChangeOrganizationMemberRoleRequest("owner"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_CannotCreateOwnerInvitation()
    {
        var ownerId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var org = await CreateOrganizationAsync(ownerId, "Acme", "acme");
        await AddMemberAsync(org.Id, adminId, OrganizationRole.Admin);
        using var adminClient = fixture.CreateAuthenticatedClient(adminId, "admin@example.com", "Admin");

        var response = await adminClient.PostAsJsonAsync(
            $"/v1/organizations/{org.Slug}/invitations",
            new CreateOrganizationInvitationRequest("alt@example.com", "owner"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_CannotDeleteOrganization()
    {
        var ownerId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var org = await CreateOrganizationAsync(ownerId, "Acme", "acme");
        await AddMemberAsync(org.Id, adminId, OrganizationRole.Admin);
        using var adminClient = fixture.CreateAuthenticatedClient(adminId, "admin@example.com", "Admin");

        var response = await adminClient.DeleteAsync($"/v1/organizations/{org.Slug}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task OrganizationInvite_CanRegisterUserAndCannotBeReplayed()
    {
        var ownerId = Guid.NewGuid();
        var org = await CreateOrganizationAsync(ownerId, "Acme", "acme");
        using var ownerClient = fixture.CreateAuthenticatedClient(ownerId, "owner@example.com", "Owner");
        var inviteResponse = await ownerClient.PostAsJsonAsync(
            $"/v1/organizations/{org.Slug}/invitations",
            new CreateOrganizationInvitationRequest("new@example.com", "member"));
        inviteResponse.EnsureSuccessStatusCode();
        var invite = await inviteResponse.Content.ReadFromJsonAsync<CreateOrganizationInvitationResponse>();
        Assert.NotNull(invite);

        using var anonymous = fixture.CreateAnonymousClient();
        var register = await anonymous.PostAsJsonAsync(
            "/v1/users/register",
            new RegisterRequest("new@example.com", "Password1!", "New User", OrganizationInvitationToken: invite.RawToken));

        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var replay = await anonymous.PostAsJsonAsync(
            "/v1/users/register",
            new RegisterRequest("another@example.com", "Password1!", "Another", OrganizationInvitationToken: invite.RawToken));
        Assert.NotEqual(HttpStatusCode.Created, replay.StatusCode);

        var members = await fixture.QueryDbAsync<OrganizationsDbContext, int>((db, ct) =>
            db.Memberships.CountAsync(m => m.OrganizationId == org.Id && m.IsActive, ct));
        Assert.Equal(2, members);
    }

    [Fact]
    public async Task ListMyOrganizations_ReturnsMembershipPermissions()
    {
        var ownerId = Guid.NewGuid();
        var org = await CreateOrganizationAsync(ownerId, "Acme", "acme");
        using var client = fixture.CreateAuthenticatedClient(ownerId, "owner@example.com", "Owner");

        var response = await client.GetAsync("/v1/organizations/my");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ListMyOrganizationsResponse>();
        Assert.NotNull(body);
        var organization = Assert.Single(body.Organizations);
        Assert.Equal(org.Id.Value, organization.OrganizationId);
        Assert.Equal("owner", organization.Role);
        Assert.Contains("organizations.members.manage", organization.Permissions);
    }

    [Fact]
    public async Task PlatformAdminWhoIsMember_GetsScopedPermissionAccessMode()
    {
        var userId = Guid.NewGuid();
        var org = await CreateOrganizationAsync(userId, "Acme", "acme");
        using var client = fixture.CreateAuthenticatedClient(userId, "admin@example.com", "Admin", role: "admin");

        var response = await client.GetAsync($"/v1/organizations/{org.Slug}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<GetOrganizationResponse>();
        Assert.NotNull(body);
        Assert.Equal("ScopedPermission", body.AccessMode);
    }

    [Fact]
    public async Task UserErasureCheck_BlocksOwners()
    {
        using var anonymous = fixture.CreateAnonymousClient();
        var register = await anonymous.PostAsJsonAsync(
            "/v1/users/register",
            new RegisterRequest("owner@example.com", "Password1!", "Owner"));
        register.EnsureSuccessStatusCode();
        var registered = await register.Content.ReadFromJsonAsync<RegisterResponse>();
        Assert.NotNull(registered);

        using var client = fixture.CreateAuthenticatedClient(registered.UserId, "owner@example.com", "Owner");
        var create = await client.PostAsJsonAsync(
            "/v1/organizations",
            new CreateOrganizationRequest("Acme", "acme"));
        create.EnsureSuccessStatusCode();

        var response = await client.DeleteAsync("/v1/users/me");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Organizations.Owner.UserErasureBlocked", problem.RootElement.GetProperty("errorCode").GetString());
        var blocker = Assert.Single(problem.RootElement.GetProperty("blockingOrganizations").EnumerateArray());
        Assert.Equal("acme", blocker.GetProperty("slug").GetString());
        Assert.True(blocker.GetProperty("isSoleOwner").GetBoolean());
    }

    [Fact]
    public async Task LastOwner_CannotLeaveOrganization()
    {
        var ownerId = Guid.NewGuid();
        var org = await CreateOrganizationAsync(ownerId, "Acme", "acme");
        using var client = fixture.CreateAuthenticatedClient(ownerId, "owner@example.com", "Owner");

        var response = await client.DeleteAsync($"/v1/organizations/{org.Slug}/members/{ownerId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Viewer_CanLeaveOrganization()
    {
        var ownerId = Guid.NewGuid();
        var viewerId = Guid.NewGuid();
        var org = await CreateOrganizationAsync(ownerId, "Acme", "acme");
        await AddMemberAsync(org.Id, viewerId, OrganizationRole.Viewer);
        using var client = fixture.CreateAuthenticatedClient(viewerId, "viewer@example.com", "Viewer");

        var response = await client.DeleteAsync($"/v1/organizations/{org.Slug}/members/{viewerId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var isActive = await fixture.QueryDbAsync<OrganizationsDbContext, bool>((db, ct) =>
            db.Memberships.AnyAsync(m => m.OrganizationId == org.Id && m.UserId == viewerId && m.IsActive, ct));
        Assert.False(isActive);
    }

    [Fact]
    public async Task DeletingMemberAccount_AnonymizesOrganizationMembership()
    {
        var ownerId = Guid.NewGuid();
        var org = await CreateOrganizationAsync(ownerId, "Acme", "acme");
        using var anonymous = fixture.CreateAnonymousClient();
        var register = await anonymous.PostAsJsonAsync(
            "/v1/users/register",
            new RegisterRequest("member@example.com", "Password1!", "Member"));
        register.EnsureSuccessStatusCode();
        var registered = await register.Content.ReadFromJsonAsync<RegisterResponse>();
        Assert.NotNull(registered);
        await AddMemberAsync(org.Id, registered.UserId, OrganizationRole.Member);
        using var memberClient = fixture.CreateAuthenticatedClient(registered.UserId, "member@example.com", "Member");
        HttpResponseMessage? response = null;

        Func<IMessageContext, Task> act = async _ =>
        {
            response = await memberClient.DeleteAsync("/v1/users/me");
        };

        await fixture.ApplicationHost.TrackActivity()
            .Timeout(TimeSpan.FromSeconds(10))
            .ExecuteAndWaitAsync(act);

        Assert.Equal(HttpStatusCode.NoContent, response!.StatusCode);
        var membership = await fixture.QueryDbAsync<OrganizationsDbContext, OrganizationMembership>((db, ct) =>
            db.Memberships.SingleAsync(m => m.OrganizationId == org.Id && m.JoinedAt != default && m.Role == OrganizationRole.Member, ct));
        Assert.False(membership.IsActive);
        Assert.True(membership.IsAnonymized);
        Assert.Null(membership.UserId);
        Assert.Null(membership.RemovedByUserId);
    }

    [Fact]
    public async Task AcceptExpiredInvitation_ReturnsValidationFailure()
    {
        var ownerId = Guid.NewGuid();
        var invitedId = Guid.NewGuid();
        var org = await CreateOrganizationAsync(ownerId, "Acme", "acme");
        var rawToken = await CreateExpiredInvitationAsync(org.Id, "expired@example.com", ownerId);
        using var client = fixture.CreateAuthenticatedClient(invitedId, "expired@example.com", "Expired");

        var response = await client.PostAsJsonAsync(
            "/v1/organizations/invitations/accept",
            new { InvitationToken = rawToken });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreatingOrganization_PersistsAuditEntryWithOrganizationScope()
    {
        var ownerId = Guid.NewGuid();
        using var client = fixture.CreateAuthenticatedClient(ownerId, "owner@example.com", "Owner");
        HttpResponseMessage? response = null;

        Func<IMessageContext, Task> act = async _ =>
        {
            response = await client.PostAsJsonAsync(
                "/v1/organizations",
                new CreateOrganizationRequest("Audited", "audited"));
        };

        await fixture.ApplicationHost.TrackActivity()
            .Timeout(TimeSpan.FromSeconds(10))
            .ExecuteAndWaitAsync(act);

        Assert.Equal(HttpStatusCode.Created, response!.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CreateOrganizationResponse>();
        Assert.NotNull(body);

        var audit = await fixture.QueryDbAsync<AuditDbContext, bool>((db, ct) =>
            db.AuditEntries.AnyAsync(
                e => e.OrganizationId == body.OrganizationId && e.EventType == "organization.created",
                ct));
        Assert.True(audit);

        var auditResponse = await client.GetAsync($"/v1/organizations/{body.Slug}/audit");

        Assert.Equal(HttpStatusCode.OK, auditResponse.StatusCode);
        using var auditBody = JsonDocument.Parse(await auditResponse.Content.ReadAsStringAsync());
        Assert.Equal(body.OrganizationId, auditBody.RootElement.GetProperty("organizationId").GetGuid());
        Assert.Equal(1, auditBody.RootElement.GetProperty("total").GetInt32());
        var entry = Assert.Single(auditBody.RootElement.GetProperty("entries").EnumerateArray());
        Assert.Equal("organization.created", entry.GetProperty("eventType").GetString());
        var payload = entry.GetProperty("payload").GetString();
        Assert.NotNull(payload);
        Assert.Contains("OrganizationId", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("Audited", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("audited", payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreatingOrganizationInvitation_PersistsAuditEntryWithoutRawTokenOrEmail()
    {
        var ownerId = Guid.NewGuid();
        var org = await CreateOrganizationAsync(ownerId, "Acme", "acme");
        using var client = fixture.CreateAuthenticatedClient(ownerId, "owner@example.com", "Owner");
        HttpResponseMessage? response = null;

        Func<IMessageContext, Task> act = async _ =>
        {
            response = await client.PostAsJsonAsync(
                $"/v1/organizations/{org.Slug}/invitations",
                new CreateOrganizationInvitationRequest("invitee@example.com", "member"));
        };

        await fixture.ApplicationHost.TrackActivity()
            .Timeout(TimeSpan.FromSeconds(10))
            .ExecuteAndWaitAsync(act);

        Assert.Equal(HttpStatusCode.OK, response!.StatusCode);
        var invitation = await response.Content.ReadFromJsonAsync<CreateOrganizationInvitationResponse>();
        Assert.NotNull(invitation);

        var payload = await fixture.QueryDbAsync<AuditDbContext, string>((db, ct) =>
            db.AuditEntries
                .Where(e => e.OrganizationId == org.Id.Value && e.EventType == "organization.invitation_created")
                .Select(e => e.Payload)
                .SingleAsync(ct));

        Assert.Contains(invitation.InvitationId.ToString(), payload, StringComparison.Ordinal);
        Assert.DoesNotContain(invitation.RawToken, payload, StringComparison.Ordinal);
        Assert.DoesNotContain("invitee@example.com", payload, StringComparison.Ordinal);
    }

    private async Task<(OrganizationId Id, string Slug)> CreateOrganizationAsync(Guid ownerId, string name, string slug)
    {
        await fixture.ExecuteDbAsync<OrganizationsDbContext>(async (db, ct) =>
        {
            var clock = fixture.Services.GetRequiredService<Modulith.Shared.Kernel.Interfaces.IClock>();
            var organization = Organization.Create(name, OrganizationSlug.Create(slug).Value, ownerId, clock).Value;
            db.Organizations.Add(organization);
            await db.SaveChangesAsync(ct);
        });

        var organizationId = await fixture.QueryDbAsync<OrganizationsDbContext, OrganizationId>((db, ct) =>
            db.Organizations
                .Where(o => o.Slug == OrganizationSlug.Create(slug).Value)
                .Select(o => o.Id)
                .SingleAsync(ct));

        return (organizationId, slug);
    }

    private async Task AddMemberAsync(OrganizationId organizationId, Guid userId, OrganizationRole role)
    {
        await fixture.ExecuteDbAsync<OrganizationsDbContext>(async (db, ct) =>
        {
            var clock = fixture.Services.GetRequiredService<Modulith.Shared.Kernel.Interfaces.IClock>();
            var organization = await db.Organizations.Include(o => o.Memberships).SingleAsync(o => o.Id == organizationId, ct);
            var add = organization.AddMember(userId, role, clock);
            Assert.False(add.IsError);
            await db.SaveChangesAsync(ct);
        });
    }

    private async Task<string> CreateExpiredInvitationAsync(OrganizationId organizationId, string email, Guid invitedByUserId)
    {
        string? rawToken = null;
        await fixture.ExecuteDbAsync<OrganizationsDbContext>(async (db, ct) =>
        {
            var clock = fixture.Services.GetRequiredService<Modulith.Shared.Kernel.Interfaces.IClock>();
            var invitation = OrganizationInvitation.Create(
                organizationId,
                email,
                OrganizationRole.Member,
                TimeSpan.FromMilliseconds(1),
                invitedByUserId,
                clock).Value;
            rawToken = invitation.RawToken;
            db.Invitations.Add(invitation.Invitation);
            await db.SaveChangesAsync(ct);
            await Task.Delay(20, ct);
        });

        return rawToken!;
    }
}
