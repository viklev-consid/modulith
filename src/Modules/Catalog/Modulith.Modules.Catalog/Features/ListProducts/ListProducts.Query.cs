namespace Modulith.Modules.Catalog.Features.ListProducts;

public sealed record ListProductsQuery(bool ActiveOnly = true, int Page = 1, int PageSize = 20);
