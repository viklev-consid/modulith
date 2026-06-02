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
            async (IMessageBus bus, CancellationToken ct, bool activeOnly = true, int page = 1, int pageSize = 20) =>
            {
                var query = new ListProductsQuery(activeOnly, page, pageSize);
                var result = await bus.InvokeAsync<ErrorOr.ErrorOr<ListProductsResponse>>(query, ct);
                return result.ToProblemDetailsOr(r => Results.Ok(r));
            })
        .WithName("ListProducts")
        .WithSummary("List products.")
        .Produces<ListProductsResponse>()
        .AllowAnonymous();
}
