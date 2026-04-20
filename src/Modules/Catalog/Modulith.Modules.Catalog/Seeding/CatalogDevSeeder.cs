using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Catalog.Domain;
using Modulith.Modules.Catalog.Persistence;
using Modulith.Shared.Infrastructure.Seeding;

namespace Modulith.Modules.Catalog.Seeding;

internal sealed class CatalogDevSeeder(CatalogDbContext db) : IModuleSeeder
{
    private static readonly (string Sku, string Name, decimal Price, string Currency)[] SeedProducts =
    [
        ("WIDGET-001", "Basic Widget",    9.99m,  "USD"),
        ("WIDGET-002", "Premium Widget",  49.99m, "USD"),
        ("GADGET-001", "Starter Gadget",  19.99m, "USD"),
    ];

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        foreach (var (rawSku, name, price, currency) in SeedProducts)
        {
            var skuResult = Sku.Create(rawSku);
            if (skuResult.IsError) continue;

            if (await db.Products.AnyAsync(p => p.Sku == skuResult.Value, cancellationToken))
                continue;

            var priceResult = Money.Create(price, currency);
            if (priceResult.IsError) continue;

            var productResult = Product.Create(skuResult.Value, name, priceResult.Value);
            if (productResult.IsError) continue;

            db.Products.Add(productResult.Value);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
