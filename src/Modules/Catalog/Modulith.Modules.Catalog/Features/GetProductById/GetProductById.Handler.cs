using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Catalog.Domain;
using Modulith.Modules.Catalog.Errors;
using Modulith.Modules.Catalog.Persistence;

namespace Modulith.Modules.Catalog.Features.GetProductById;

public sealed class GetProductByIdHandler(CatalogDbContext db)
{
    public async Task<ErrorOr<GetProductByIdResponse>> Handle(GetProductByIdQuery query, CancellationToken ct)
    {
        var productId = new ProductId(query.ProductId);

        var product = await db.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == productId, ct);

        if (product is null)
        {
            return CatalogErrors.ProductNotFound;
        }

        return new GetProductByIdResponse(
            product.Id.Value,
            product.Sku.Value,
            product.Name,
            product.Price.Amount,
            product.Price.Currency,
            product.IsActive);
    }
}
