namespace Modulith.Modules.Catalog.Contracts.Events;

public sealed record ProductCreatedV1(Guid ProductId, string Sku, string Name, decimal Price, string Currency);
