namespace Modulith.Modules.Catalog.Features.CreateProduct;

public sealed record CreateProductCommand(string Sku, string Name, decimal Price, string Currency);
