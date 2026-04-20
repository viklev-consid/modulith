namespace Modulith.Modules.Catalog.Features.CreateProduct;

public sealed record CreateProductResponse(Guid ProductId, string Sku, string Name, decimal Price, string Currency);
