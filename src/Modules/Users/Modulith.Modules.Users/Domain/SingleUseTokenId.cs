using Modulith.Shared.Kernel.Identifiers;

namespace Modulith.Modules.Users.Domain;

public sealed record SingleUseTokenId(Guid Value) : TypedId<Guid>(Value)
{
    public static SingleUseTokenId New() => new(Guid.NewGuid());
}
