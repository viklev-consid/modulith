using Modulith.Shared.Kernel.Domain;

namespace Modulith.Modules.Catalog.Domain.Events;

internal sealed record ProductCreated(ProductId ProductId, string Sku, string Name) : DomainEvent;
