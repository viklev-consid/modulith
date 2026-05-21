using Modulith.Modules.Organizations.Domain;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Organizations.UnitTests.Domain;

[Trait("Category", "Unit")]
public sealed class OrganizationTests
{
    private static readonly Guid ownerId = Guid.NewGuid();
    private readonly TestClock clock = new(DateTimeOffset.UtcNow);

    [Fact]
    public void Create_AddsCreatorAsOwner()
    {
        var slug = OrganizationSlug.Create("acme").Value;

        var result = Organization.Create("Acme", slug, ownerId, clock);

        Assert.False(result.IsError);
        var organization = result.Value;
        var owner = Assert.Single(organization.Memberships);
        Assert.Equal(ownerId, owner.UserId);
        Assert.Equal(OrganizationRole.Owner, owner.Role);
        Assert.True(owner.IsActive);
    }

    [Fact]
    public void RemoveMember_WhenLastOwner_ReturnsError()
    {
        var organization = CreateOrganization();

        var result = organization.RemoveMember(ownerId, ownerId, clock);

        Assert.True(result.IsError);
        Assert.True(organization.FindActiveMembership(ownerId)?.IsActive);
    }

    [Fact]
    public void ChangeMemberRole_WhenLastOwnerWouldBeDemoted_ReturnsError()
    {
        var organization = CreateOrganization();

        var result = organization.ChangeMemberRole(ownerId, OrganizationRole.Admin);

        Assert.True(result.IsError);
        Assert.Equal(OrganizationRole.Owner, organization.FindActiveMembership(ownerId)?.Role);
    }

    [Fact]
    public void Delete_WhenLastOwner_SucceedsAndRemovesMemberships()
    {
        var organization = CreateOrganization();

        var result = organization.Delete(ownerId, clock);

        Assert.False(result.IsError);
        Assert.True(organization.IsDeleted);
        Assert.All(organization.Memberships, m => Assert.False(m.IsActive));
    }

    [Fact]
    public void AddMember_WhenAlreadyActive_ReturnsError()
    {
        var organization = CreateOrganization();
        var userId = Guid.NewGuid();
        organization.AddMember(userId, OrganizationRole.Member, clock);

        var result = organization.AddMember(userId, OrganizationRole.Admin, clock);

        Assert.True(result.IsError);
    }

    private Organization CreateOrganization() =>
        Organization.Create("Acme", OrganizationSlug.Create("acme").Value, ownerId, clock).Value;
}

internal sealed class TestClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow { get; private set; } = now;
}
