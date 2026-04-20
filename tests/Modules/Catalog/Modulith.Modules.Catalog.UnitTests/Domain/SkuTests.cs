using Modulith.Modules.Catalog.Domain;

namespace Modulith.Modules.Catalog.UnitTests.Domain;

[Trait("Category", "Unit")]
public sealed class SkuTests
{
    [Fact]
    public void Create_WithValidValue_ReturnsSku()
    {
        var result = Sku.Create("widget-001");

        Assert.False(result.IsError);
        Assert.Equal("WIDGET-001", result.Value.Value);
    }

    [Fact]
    public void Create_NormalizesToUppercase()
    {
        var result = Sku.Create("lower-case");

        Assert.False(result.IsError);
        Assert.Equal("LOWER-CASE", result.Value.Value);
    }

    [Fact]
    public void Create_TrimsWhitespace()
    {
        var result = Sku.Create("  SKU  ");

        Assert.False(result.IsError);
        Assert.Equal("SKU", result.Value.Value);
    }

    [Fact]
    public void Create_WithNull_ReturnsError()
    {
        var result = Sku.Create(null);

        Assert.True(result.IsError);
    }

    [Fact]
    public void Create_WithEmptyString_ReturnsError()
    {
        var result = Sku.Create("");

        Assert.True(result.IsError);
    }

    [Fact]
    public void Create_WithValueTooLong_ReturnsError()
    {
        var result = Sku.Create(new string('A', 51));

        Assert.True(result.IsError);
    }

    [Fact]
    public void Equality_SameValue_AreEqual()
    {
        var sku1 = Sku.Create("WIDGET-001").Value;
        var sku2 = Sku.Create("WIDGET-001").Value;

        Assert.Equal(sku1, sku2);
    }
}
