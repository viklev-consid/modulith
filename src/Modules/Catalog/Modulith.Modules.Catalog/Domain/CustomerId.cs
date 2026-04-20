using Modulith.Shared.Kernel.Identifiers;

namespace Modulith.Modules.Catalog.Domain;

public sealed record CustomerId(Guid Value) : TypedId<Guid>(Value)
{
    public static CustomerId New() => new(Guid.NewGuid());
}
