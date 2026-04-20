namespace Modulith.Modules.Catalog.Features.CreateProduct;

public sealed record CreateProductRequest(string Sku, string Name, decimal Price, string Currency = "USD");
