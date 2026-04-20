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
                var result = await bus.InvokeAsync<ErrorOr.ErrorOr<ListProductsResponse>>(query, ct);
                return result.ToProblemDetailsOr(r => Results.Ok(r));
            })
        .WithName("ListProducts")
        .WithSummary("List all products.")
        .Produces<ListProductsResponse>()
        .AllowAnonymous();
}
