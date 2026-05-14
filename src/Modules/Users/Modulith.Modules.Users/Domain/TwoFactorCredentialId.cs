using Modulith.Shared.Kernel.Identifiers;

namespace Modulith.Modules.Users.Domain;

public sealed record TwoFactorCredentialId(Guid Value) : TypedId<Guid>(Value)
{
    public static TwoFactorCredentialId New() => new(Guid.NewGuid());
}
