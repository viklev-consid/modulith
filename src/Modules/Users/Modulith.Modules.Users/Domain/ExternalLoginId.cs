using Modulith.Shared.Kernel.Identifiers;

namespace Modulith.Modules.Users.Domain;

public sealed record ExternalLoginId(Guid Value) : TypedId<Guid>(Value)
{
    public static ExternalLoginId New() => new(Guid.NewGuid());
}
