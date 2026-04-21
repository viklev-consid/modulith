using Modulith.Shared.Kernel.Identifiers;

namespace Modulith.Modules.Users.Domain;

public sealed record PendingEmailChangeId(Guid Value) : TypedId<Guid>(Value)
{
    public static PendingEmailChangeId New() => new(Guid.NewGuid());
}
