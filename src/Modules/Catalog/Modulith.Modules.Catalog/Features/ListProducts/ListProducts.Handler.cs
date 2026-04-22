using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Catalog.Persistence;

namespace Modulith.Modules.Catalog.Features.ListProducts;

public sealed class ListProductsHandler(CatalogDbContext db)
{
    public async Task<ErrorOr<ListProductsResponse>> Handle(ListProductsQuery query, CancellationToken ct)
        => await CatalogTelemetry.InstrumentAsync(nameof(ListProductsHandler), () => HandleCoreAsync(query, ct));

    private async Task<ErrorOr<ListProductsResponse>> HandleCoreAsync(ListProductsQuery query, CancellationToken ct)
    {
        var products = await db.Products
            .AsNoTracking()
            .Where(p => !query.ActiveOnly || p.IsActive)
            .OrderBy(p => p.Name)
            .Select(p => new ProductSummary(
                p.Id.Value,
                p.Sku.Value,
                p.Name,
                p.Price.Amount,
                p.Price.Currency,
                p.IsActive))
            .ToListAsync(ct);

        return new ListProductsResponse(products);
    }
}
