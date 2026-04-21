using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Catalog.Contracts.Events;
using Modulith.Modules.Catalog.Domain;
using Modulith.Modules.Catalog.Errors;
using Modulith.Modules.Catalog.Persistence;
using Wolverine;

namespace Modulith.Modules.Catalog.Features.CreateProduct;

public sealed class CreateProductHandler(CatalogDbContext db, IMessageBus bus)
{
    public async Task<ErrorOr<CreateProductResponse>> Handle(CreateProductCommand cmd, CancellationToken ct)
        => await CatalogTelemetry.InstrumentAsync(nameof(CreateProductHandler), () => HandleCoreAsync(cmd, ct));

    private async Task<ErrorOr<CreateProductResponse>> HandleCoreAsync(CreateProductCommand cmd, CancellationToken ct)
    {
        var skuResult = Sku.Create(cmd.Sku);
        if (skuResult.IsError)
        {
            return skuResult.Errors;
        }

        var sku = skuResult.Value;

        if (await db.Products.AnyAsync(p => p.Sku == sku, ct))
        {
            return CatalogErrors.SkuAlreadyExists;
        }

        var priceResult = Money.Create(cmd.Price, cmd.Currency);
        if (priceResult.IsError)
        {
            return priceResult.Errors;
        }

        var productResult = Product.Create(sku, cmd.Name, priceResult.Value);
        if (productResult.IsError)
        {
            return productResult.Errors;
        }

        var product = productResult.Value;
        db.Products.Add(product);
        await db.SaveChangesAsync(ct);

        await bus.PublishAsync(new ProductCreatedV1(
            product.Id.Value,
            product.Sku.Value,
            product.Name,
            product.Price.Amount,
            product.Price.Currency));
        CatalogTelemetry.EventsPublished.Add(1, new KeyValuePair<string, object?>("event", nameof(ProductCreatedV1)));

        return new CreateProductResponse(
            product.Id.Value,
            product.Sku.Value,
            product.Name,
            product.Price.Amount,
            product.Price.Currency);
    }
}
