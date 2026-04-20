using ErrorOr;
using Modulith.Modules.Catalog.Domain.Events;
using Modulith.Modules.Catalog.Errors;
using Modulith.Shared.Kernel.Domain;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Catalog.Domain;

public sealed class Product : AggregateRoot<ProductId>, IAuditableEntity
{
    private Product(ProductId id, Sku sku, string name, Money price)
        : base(id)
    {
        Sku = sku;
        Name = name;
        Price = price;
        IsActive = true;
    }

    // Required by EF Core for materialization.
    private Product() : base(default!) { }

    public Sku Sku { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public Money Price { get; private set; } = null!;
    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public string? UpdatedBy { get; private set; }

    public static ErrorOr<Product> Create(Sku sku, string name, Money price)
    {
        if (string.IsNullOrWhiteSpace(name))
            return CatalogErrors.ProductNameEmpty;

        if (name.Length > 200)
            return CatalogErrors.ProductNameTooLong;

        var product = new Product(ProductId.New(), sku, name.Trim(), price);
        product.RaiseEvent(new ProductCreated(product.Id, sku.Value, name.Trim()));
        return product;
    }

    public ErrorOr<Success> Deactivate()
    {
        if (!IsActive)
            return CatalogErrors.ProductAlreadyInactive;

        IsActive = false;
        RaiseEvent(new ProductDeactivated(Id));
        return Result.Success;
    }

    public ErrorOr<Success> UpdatePrice(Money newPrice)
    {
        var oldAmount = Price.Amount;
        Price = newPrice;
        RaiseEvent(new ProductPriceUpdated(Id, oldAmount, newPrice.Amount, newPrice.Currency));
        return Result.Success;
    }
}
