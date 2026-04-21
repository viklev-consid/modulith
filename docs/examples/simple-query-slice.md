# Example: Simple Query Slice

**Pattern:** Read-only slice — no mutation, no validator, public endpoint.

**Source:** `src/Modules/Catalog/Modulith.Modules.Catalog/Features/ListProducts/`

---

## The six files

### `ListProducts.Query.cs`

```csharp
namespace Modulith.Modules.Catalog.Features.ListProducts;

public sealed record ListProductsQuery(bool ActiveOnly = true);
```

Query records (not `Command`) — signals no state mutation. Default parameter makes the field optional in HTTP bindings.

### `ListProducts.Response.cs`

```csharp
namespace Modulith.Modules.Catalog.Features.ListProducts;

public sealed record ListProductsResponse(IReadOnlyList<ProductSummary> Products);

public sealed record ProductSummary(
    Guid Id,
    string Sku,
    string Name,
    decimal Price,
    string Currency,
    bool IsActive);
```

Flat DTOs with primitive types — no domain types (`Sku`, `Money`) escape the module boundary.

### `ListProducts.Handler.cs`

```csharp
using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Catalog.Persistence;

namespace Modulith.Modules.Catalog.Features.ListProducts;

public sealed class ListProductsHandler(CatalogDbContext db)
{
    public async Task<ErrorOr<ListProductsResponse>> Handle(ListProductsQuery query, CancellationToken ct)
    {
        var products = await db.Products
            .AsNoTracking()                             // read-only — no change tracking overhead
            .Where(p => !query.ActiveOnly || p.IsActive)
            .OrderBy(p => p.Name)
            .Select(p => new ProductSummary(            // project in SQL — don't materialise full entities
                p.Id.Value,
                p.Sku.Value,
                p.Name,
                p.Price.Amount,
                p.Price.Currency,
                p.IsActive))
            .ToListAsync(ct);

        return new ListProductsResponse(products);
    }
}
```

Key choices:
- `AsNoTracking()` on every read-only query.
- `.Select(...)` projects in SQL — no full entity hydration.
- Always returns `ErrorOr<T>` even when failure is impossible — consistency over cleverness.

### `ListProducts.Endpoint.cs`

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modulith.Shared.Infrastructure.Http;
using Wolverine;

namespace Modulith.Modules.Catalog.Features.ListProducts;

internal static class ListProductsEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet(CatalogRoutes.Products,
            async (IMessageBus bus, CancellationToken ct, bool activeOnly = true) =>
            {
                var query = new ListProductsQuery(activeOnly);
                var result = await bus.InvokeAsync<ErrorOr<ListProductsResponse>>(query, ct);
                return result.ToProblemDetailsOr(r => Results.Ok(r));
            })
        .WithName("ListProducts")
        .WithSummary("List all products.")
        .Produces<ListProductsResponse>()
        .AllowAnonymous();
}
```

Key choices:
- No `Validator` file for this slice — query params are primitives with no cross-field rules; `bool activeOnly` needs no validation.
- `AllowAnonymous()` — the catalog is public.
- Route from `CatalogRoutes.Products` (a constant) — never inline strings.
- `ToProblemDetailsOr(r => Results.Ok(r))` is the shared mapping extension.

### No `Validator.cs`

Not every slice needs a validator. Omit it when there is no input to validate (query-only endpoint binding primitives from query string). The scaffold still creates the file stub; delete it if empty.

---

## When to use this pattern

- Listing / search / filter endpoints.
- No authentication required (or simple policy — just add `.RequireAuthorization()`).
- Handler result can never be an error (though you still return `ErrorOr<T>` for consistency).

---

## Related

- [`../how-to/add-a-slice.md`](../how-to/add-a-slice.md)
- [`../adr/0002-vertical-slice-architecture.md`](../adr/0002-vertical-slice-architecture.md)
