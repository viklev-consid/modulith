using Modulith.Shared.Kernel.Identifiers;

namespace Modulith.Modules.Users.Domain;

public sealed record RecoveryCodeId(Guid Value) : TypedId<Guid>(Value)
{
    public static RecoveryCodeId New() => new(Guid.NewGuid());
}
