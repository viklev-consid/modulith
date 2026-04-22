namespace Modulith.Modules.Catalog.Contracts.Authorization;

public static class CatalogPermissions
{
    public const string ProductsRead  = "catalog.products.read";
    public const string ProductsWrite = "catalog.products.write";

    public static IReadOnlyCollection<string> All { get; } =
        [ProductsRead, ProductsWrite];
}
