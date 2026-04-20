using ErrorOr;

namespace Modulith.Modules.Catalog.Errors;

internal static class CatalogErrors
{
    // Sku value object
    public static readonly Error SkuEmpty =
        Error.Validation("Catalog.Sku.Empty", "SKU cannot be empty.");

    public static readonly Error SkuTooLong =
        Error.Validation("Catalog.Sku.TooLong", "SKU cannot exceed 50 characters.");

    // Money value object
    public static readonly Error PriceNegative =
        Error.Validation("Catalog.Price.Negative", "Price cannot be negative.");

    public static readonly Error CurrencyEmpty =
        Error.Validation("Catalog.Currency.Empty", "Currency code cannot be empty.");

    public static readonly Error CurrencyInvalid =
        Error.Validation("Catalog.Currency.Invalid", "Currency code must be a 3-letter ISO 4217 code.");

    // Product aggregate
    public static readonly Error ProductNameEmpty =
        Error.Validation("Catalog.Product.NameEmpty", "Product name cannot be empty.");

    public static readonly Error ProductNameTooLong =
        Error.Validation("Catalog.Product.NameTooLong", "Product name cannot exceed 200 characters.");

    public static readonly Error SkuAlreadyExists =
        Error.Conflict("Catalog.Product.SkuAlreadyExists", "A product with this SKU already exists.");

    public static readonly Error ProductNotFound =
        Error.NotFound("Catalog.Product.NotFound", "Product was not found.");

    public static readonly Error ProductAlreadyInactive =
        Error.Conflict("Catalog.Product.AlreadyInactive", "Product is already inactive.");
}
