using Modulith.Shared.Kernel.Identifiers;

namespace Modulith.Modules.Organizations.Domain;

public sealed record OrganizationMembershipId(Guid Value) : TypedId<Guid>(Value)
{
    public static OrganizationMembershipId New() => new(Guid.NewGuid());
}
