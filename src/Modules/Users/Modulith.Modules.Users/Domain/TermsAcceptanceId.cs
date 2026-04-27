using Modulith.Shared.Kernel.Identifiers;

namespace Modulith.Modules.Users.Domain;

public sealed record TermsAcceptanceId(Guid Value) : TypedId<Guid>(Value)
{
    public static TermsAcceptanceId New() => new(Guid.NewGuid());
}
