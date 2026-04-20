using Modulith.Modules.Catalog.Domain;

namespace Modulith.Modules.Catalog.UnitTests.Domain;

[Trait("Category", "Unit")]
public sealed class MoneyTests
{
    [Fact]
    public void Create_WithValidArguments_ReturnsMoney()
    {
        var result = Money.Create(9.99m, "USD");

        Assert.False(result.IsError);
        Assert.Equal(9.99m, result.Value.Amount);
        Assert.Equal("USD", result.Value.Currency);
    }

    [Fact]
    public void Create_NormalizesToUppercase()
    {
        var result = Money.Create(1m, "usd");

        Assert.False(result.IsError);
        Assert.Equal("USD", result.Value.Currency);
    }

    [Fact]
    public void Create_WithNegativeAmount_ReturnsError()
    {
        var result = Money.Create(-1m);

        Assert.True(result.IsError);
    }

    [Fact]
    public void Create_WithZeroAmount_Succeeds()
    {
        var result = Money.Create(0m);

        Assert.False(result.IsError);
    }

    [Fact]
    public void Create_WithNullCurrency_ReturnsError()
    {
        var result = Money.Create(1m, null);

        Assert.True(result.IsError);
    }

    [Fact]
    public void Create_WithInvalidCurrencyLength_ReturnsError()
    {
        var result = Money.Create(1m, "US");

        Assert.True(result.IsError);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var m1 = Money.Create(9.99m, "USD").Value;
        var m2 = Money.Create(9.99m, "USD").Value;

        Assert.Equal(m1, m2);
    }
}
