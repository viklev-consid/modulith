using Modulith.Shared.Kernel.Identifiers;

namespace Modulith.Modules.Organizations.Domain;

public sealed record OrganizationId(Guid Value) : TypedId<Guid>(Value)
{
    public static OrganizationId New() => new(Guid.NewGuid());
}
