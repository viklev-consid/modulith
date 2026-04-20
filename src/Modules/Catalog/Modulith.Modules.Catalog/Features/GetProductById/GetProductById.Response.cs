namespace Modulith.Modules.Catalog.Features.GetProductById;

public sealed record GetProductByIdResponse(
    Guid Id,
    string Sku,
    string Name,
    decimal Price,
    string Currency,
    bool IsActive);
