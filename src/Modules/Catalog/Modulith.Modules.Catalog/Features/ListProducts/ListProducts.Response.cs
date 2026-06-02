namespace Modulith.Modules.Catalog.Features.ListProducts;

public sealed record ListProductsResponse(IReadOnlyList<ProductSummary> Products, int Total, int Page, int PageSize);

public sealed record ProductSummary(
    Guid Id,
    string Sku,
    string Name,
    decimal Price,
    string Currency,
    bool IsActive);
