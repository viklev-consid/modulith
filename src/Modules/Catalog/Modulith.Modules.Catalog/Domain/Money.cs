using ErrorOr;
using Modulith.Modules.Catalog.Errors;

namespace Modulith.Modules.Catalog.Domain;

public sealed record Money
{
    public decimal Amount { get; }
    public string Currency { get; }

    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public static ErrorOr<Money> Create(decimal amount, string? currency = "USD")
    {
        if (amount < 0)
        {
            return CatalogErrors.PriceNegative;
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            return CatalogErrors.CurrencyEmpty;
        }

        var normalized = currency.Trim().ToUpperInvariant();

        if (normalized.Length != 3)
        {
            return CatalogErrors.CurrencyInvalid;
        }

        return new Money(amount, normalized);
    }

    public override string ToString() => $"{Amount:F2} {Currency}";
}
