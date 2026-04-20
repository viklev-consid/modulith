using Modulith.Shared.Kernel.Domain;

namespace Modulith.Modules.Catalog.Domain.Events;

internal sealed record ProductPriceUpdated(ProductId ProductId, decimal OldAmount, decimal NewAmount, string Currency) : DomainEvent;
