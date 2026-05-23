using ErrorOr;
using Modulith.Modules.Organizations.Errors;
using Modulith.Shared.Kernel.Domain;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Organizations.Domain;

public sealed class Organization : AggregateRoot<OrganizationId>, IAuditableEntity
{
    private readonly List<OrganizationMembership> memberships = [];

    private Organization(
        OrganizationId id,
        string name,
        OrganizationSlug slug) : base(id)
    {
        Name = name;
        Slug = slug;
    }

    private Organization() : base(default!) { }

    public string Name { get; private set; } = null!;
    public OrganizationSlug Slug { get; private set; } = null!;
    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public Guid? DeletedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public string? UpdatedBy { get; private set; }
    public IReadOnlyCollection<OrganizationMembership> Memberships => memberships;

    public static ErrorOr<Organization> Create(
        string name,
        OrganizationSlug slug,
        Guid createdByUserId,
        IClock clock)
    {
        var nameResult = NormalizeName(name);
        if (nameResult.IsError)
        {
            return nameResult.Errors;
        }

        var organization = new Organization(OrganizationId.New(), nameResult.Value, slug);
        organization.memberships.Add(OrganizationMembership.Create(
            organization.Id,
            createdByUserId,
            OrganizationRole.Owner,
            clock));

        return organization;
    }

    public ErrorOr<Success> Update(string name, OrganizationSlug slug)
    {
        if (IsDeleted)
        {
            return OrganizationsErrors.OrganizationDeleted;
        }

        var nameResult = NormalizeName(name);
        if (nameResult.IsError)
        {
            return nameResult.Errors;
        }

        Name = nameResult.Value;
        Slug = slug;
        return Result.Success;
    }

    public ErrorOr<OrganizationMembership> AddMember(Guid userId, OrganizationRole role, IClock clock)
    {
        if (IsDeleted)
        {
            return OrganizationsErrors.OrganizationDeleted;
        }

        if (memberships.Any(m => m.UserId == userId && m.IsActive))
        {
            return OrganizationsErrors.MemberAlreadyExists;
        }

        var membership = OrganizationMembership.Create(Id, userId, role, clock);
        memberships.Add(membership);
        return membership;
    }

    public ErrorOr<Success> ChangeMemberRole(Guid userId, OrganizationRole role)
    {
        if (IsDeleted)
        {
            return OrganizationsErrors.OrganizationDeleted;
        }

        var membership = FindActiveMembership(userId);
        if (membership is null)
        {
            return OrganizationsErrors.MemberNotFound;
        }

        if (membership.Role == OrganizationRole.Owner && role != OrganizationRole.Owner && CountActiveOwners() == 1)
        {
            return OrganizationsErrors.LastOwnerRequired;
        }

        membership.ChangeRole(role);
        return Result.Success;
    }

    public ErrorOr<string> ChangeMemberRole(Guid actorUserId, Guid targetUserId, OrganizationRole role)
    {
        var actor = FindActiveMembership(actorUserId);
        if (actor is null)
        {
            return OrganizationsErrors.MemberNotFound;
        }

        var target = FindActiveMembership(targetUserId);
        if (target is null)
        {
            return OrganizationsErrors.MemberNotFound;
        }

        var requiredRank = Math.Max(target.Role.Rank, role.Rank);
        if (actor.Role.Rank < requiredRank)
        {
            return OrganizationsErrors.RoleEscalationForbidden;
        }

        var oldRole = target.Role.Name;
        var change = ChangeMemberRole(targetUserId, role);
        return change.IsError ? change.Errors : oldRole;
    }

    public ErrorOr<Success> EnsureCanInviteRole(Guid actorUserId, OrganizationRole role)
    {
        var actor = FindActiveMembership(actorUserId);
        if (actor is null)
        {
            return OrganizationsErrors.MemberNotFound;
        }

        return actor.Role.Rank >= role.Rank
            ? Result.Success
            : OrganizationsErrors.RoleEscalationForbidden;
    }

    public ErrorOr<Success> RemoveMember(Guid userId, Guid removedByUserId, IClock clock)
    {
        if (IsDeleted)
        {
            return OrganizationsErrors.OrganizationDeleted;
        }

        var membership = FindActiveMembership(userId);
        if (membership is null)
        {
            return OrganizationsErrors.MemberNotFound;
        }

        if (membership.Role == OrganizationRole.Owner && CountActiveOwners() == 1)
        {
            return OrganizationsErrors.LastOwnerRequired;
        }

        membership.Remove(removedByUserId, clock);
        return Result.Success;
    }

    public ErrorOr<Success> Delete(Guid deletedByUserId, IClock clock)
    {
        if (IsDeleted)
        {
            return Result.Success;
        }

        IsDeleted = true;
        DeletedAt = clock.UtcNow;
        DeletedByUserId = deletedByUserId;

        foreach (var membership in memberships.Where(m => m.IsActive))
        {
            membership.Remove(deletedByUserId, clock);
        }

        return Result.Success;
    }

    public OrganizationMembership? FindActiveMembership(Guid userId) =>
        memberships.FirstOrDefault(m => m.UserId == userId && m.IsActive);

    public void AnonymizeUserReferences(Guid userId)
    {
        if (DeletedByUserId == userId)
        {
            DeletedByUserId = null;
        }
    }

    private int CountActiveOwners() =>
        memberships.Count(m => m.IsActive && m.Role == OrganizationRole.Owner);

    private static ErrorOr<string> NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return OrganizationsErrors.NameEmpty;
        }

        var trimmed = name.Trim();
        if (trimmed.Length > 200)
        {
            return OrganizationsErrors.NameTooLong;
        }

        return trimmed;
    }
}
