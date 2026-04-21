using ErrorOr;
using Modulith.Modules.Catalog.Errors;

namespace Modulith.Modules.Catalog.Domain;

public sealed record Sku
{
    public string Value { get; }

    private Sku(string value) => Value = value;

    public static ErrorOr<Sku> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return CatalogErrors.SkuEmpty;
        }

        var normalized = value.Trim().ToUpperInvariant();

        if (normalized.Length > 50)
        {
            return CatalogErrors.SkuTooLong;
        }

        return new Sku(normalized);
    }

    public override string ToString() => Value;
}
