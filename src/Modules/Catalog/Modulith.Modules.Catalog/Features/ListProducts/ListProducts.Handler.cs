using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Modulith.Modules.Catalog.Persistence;

namespace Modulith.Modules.Catalog.Features.ListProducts;

public sealed class ListProductsHandler(CatalogDbContext db, IOptions<CatalogOptions> options)
{
    public async Task<ErrorOr<ListProductsResponse>> Handle(ListProductsQuery query, CancellationToken ct)
        => await CatalogTelemetry.InstrumentAsync(nameof(ListProductsHandler), () => HandleCoreAsync(query, ct));

    private async Task<ErrorOr<ListProductsResponse>> HandleCoreAsync(ListProductsQuery query, CancellationToken ct)
    {
        if (query.Page < 1 ||
            query.PageSize < 1 ||
            query.PageSize > options.Value.MaxProductsPerPage ||
            query.Page > int.MaxValue / query.PageSize)
        {
            return Error.Validation(
                "Catalog.Products.PaginationInvalid",
                $"Page must be positive and page size must be between 1 and {options.Value.MaxProductsPerPage}.");
        }

        var productsQuery = db.Products
            .AsNoTracking()
            .Where(p => !query.ActiveOnly || p.IsActive);
        var total = await productsQuery.CountAsync(ct);
        var products = await productsQuery
            .OrderBy(p => p.Name)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(p => new ProductSummary(
                p.Id.Value,
                p.Sku.Value,
                p.Name,
                p.Price.Amount,
                p.Price.Currency,
                p.IsActive))
            .ToListAsync(ct);

        return new ListProductsResponse(products, total, query.Page, query.PageSize);
    }
}
