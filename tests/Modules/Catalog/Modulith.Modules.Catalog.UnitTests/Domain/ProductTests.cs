using Modulith.Modules.Catalog.Domain;

namespace Modulith.Modules.Catalog.UnitTests.Domain;

[Trait("Category", "Unit")]
public sealed class ProductTests
{
    private static Sku ValidSku => Sku.Create("WIDGET-001").Value;
    private static Money ValidPrice => Money.Create(9.99m, "USD").Value;

    [Fact]
    public void Create_WithValidArguments_ReturnsProduct()
    {
        var result = Product.Create(ValidSku, "Test Widget", ValidPrice);

        Assert.False(result.IsError);
        Assert.Equal("WIDGET-001", result.Value.Sku.Value);
        Assert.Equal("Test Widget", result.Value.Name);
        Assert.Equal(9.99m, result.Value.Price.Amount);
        Assert.True(result.Value.IsActive);
    }

    [Fact]
    public void Create_WithEmptyName_ReturnsError()
    {
        var result = Product.Create(ValidSku, "   ", ValidPrice);

        Assert.True(result.IsError);
    }

    [Fact]
    public void Create_WithNameTooLong_ReturnsError()
    {
        var result = Product.Create(ValidSku, new string('x', 201), ValidPrice);

        Assert.True(result.IsError);
    }

    [Fact]
    public void Create_RaisesDomainEvent()
    {
        var result = Product.Create(ValidSku, "Test Widget", ValidPrice);

        Assert.Single(result.Value.DomainEvents);
    }

    [Fact]
    public void Deactivate_ActiveProduct_SetsIsActiveFalse()
    {
        var product = Product.Create(ValidSku, "Test Widget", ValidPrice).Value;

        var result = product.Deactivate();

        Assert.False(result.IsError);
        Assert.False(product.IsActive);
    }

    [Fact]
    public void Deactivate_AlreadyInactiveProduct_ReturnsError()
    {
        var product = Product.Create(ValidSku, "Test Widget", ValidPrice).Value;
        product.Deactivate();

        var result = product.Deactivate();

        Assert.True(result.IsError);
    }

    [Fact]
    public void UpdatePrice_ChangesPrice()
    {
        var product = Product.Create(ValidSku, "Test Widget", ValidPrice).Value;
        var newPrice = Money.Create(19.99m, "USD").Value;

        var result = product.UpdatePrice(newPrice);

        Assert.False(result.IsError);
        Assert.Equal(19.99m, product.Price.Amount);
    }

    [Fact]
    public void Product_HasNoPublicSetters()
    {
        var publicSetters = typeof(Product)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Where(p => p.SetMethod?.IsPublic == true)
            .Select(p => p.Name)
            .ToList();

        Assert.Empty(publicSetters);
    }
}
