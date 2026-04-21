using Modulith.Shared.Kernel.Identifiers;

namespace Modulith.Modules.Users.Domain;

public sealed record RefreshTokenId(Guid Value) : TypedId<Guid>(Value)
{
    public static RefreshTokenId New() => new(Guid.NewGuid());
}
