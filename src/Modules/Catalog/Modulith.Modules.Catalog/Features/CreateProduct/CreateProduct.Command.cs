namespace Modulith.Modules.Catalog.Features.CreateProduct;

internal sealed record CreateProductCommand(string Sku, string Name, decimal Price, string Currency);
