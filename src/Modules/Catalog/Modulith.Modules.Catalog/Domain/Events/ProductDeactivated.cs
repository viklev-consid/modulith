using Modulith.Shared.Kernel.Domain;

namespace Modulith.Modules.Catalog.Domain.Events;

internal sealed record ProductDeactivated(ProductId ProductId) : DomainEvent;
