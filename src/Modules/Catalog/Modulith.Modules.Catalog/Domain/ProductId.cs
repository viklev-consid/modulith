using Modulith.Shared.Kernel.Identifiers;

namespace Modulith.Modules.Catalog.Domain;

public sealed record ProductId(Guid Value) : TypedId<Guid>(Value)
{
    public static ProductId New() => new(Guid.NewGuid());
}
