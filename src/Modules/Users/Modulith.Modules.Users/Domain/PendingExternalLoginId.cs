using Modulith.Shared.Kernel.Identifiers;

namespace Modulith.Modules.Users.Domain;

public sealed record PendingExternalLoginId(Guid Value) : TypedId<Guid>(Value)
{
    public static PendingExternalLoginId New() => new(Guid.NewGuid());
}
