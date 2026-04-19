using Modulith.Shared.Kernel.Identifiers;

namespace Modulith.Modules.Users.Domain;

public sealed record UserId(Guid Value) : TypedId<Guid>(Value)
{
    public static UserId New() => new(Guid.NewGuid());
}
