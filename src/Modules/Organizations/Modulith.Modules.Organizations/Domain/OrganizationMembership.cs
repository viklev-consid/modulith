using Modulith.Shared.Kernel.Domain;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Organizations.Domain;

public sealed class OrganizationMembership : Entity<OrganizationMembershipId>, IAuditableEntity
{
    private OrganizationMembership(
        OrganizationMembershipId id,
        OrganizationId organizationId,
        Guid userId,
        OrganizationRole role,
        DateTimeOffset joinedAt) : base(id)
    {
        OrganizationId = organizationId;
        UserId = userId;
        Role = role;
        JoinedAt = joinedAt;
        IsActive = true;
    }

    private OrganizationMembership() : base(default!) { }

    public OrganizationId OrganizationId { get; private set; } = null!;
    public Guid UserId { get; private set; }
    public OrganizationRole Role { get; private set; } = OrganizationRole.Member;
    public DateTimeOffset JoinedAt { get; private set; }
    public DateTimeOffset? RemovedAt { get; private set; }
    public Guid? RemovedByUserId { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsAnonymized { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public string? UpdatedBy { get; private set; }

    public static OrganizationMembership Create(
        OrganizationId organizationId,
        Guid userId,
        OrganizationRole role,
        IClock clock) =>
        new(OrganizationMembershipId.New(), organizationId, userId, role, clock.UtcNow);

    public void ChangeRole(OrganizationRole role)
    {
        Role = role;
    }

    public void Remove(Guid removedByUserId, IClock clock)
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        RemovedAt = clock.UtcNow;
        RemovedByUserId = removedByUserId;
    }

    public void Anonymize()
    {
        IsAnonymized = true;
    }
}
