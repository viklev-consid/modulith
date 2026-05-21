using Modulith.Shared.Kernel.Identifiers;

namespace Modulith.Modules.Organizations.Domain;

public sealed record OrganizationInvitationId(Guid Value) : TypedId<Guid>(Value)
{
    public static OrganizationInvitationId New() => new(Guid.NewGuid());
}
