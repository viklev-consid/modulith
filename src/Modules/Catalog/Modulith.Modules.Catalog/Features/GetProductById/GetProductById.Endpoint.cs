using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modulith.Shared.Infrastructure.Http;
using Wolverine;

namespace Modulith.Modules.Catalog.Features.GetProductById;

internal static class GetProductByIdEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet($"{CatalogRoutes.Products}/{{productId:guid}}",
            async (Guid productId, IMessageBus bus, CancellationToken ct) =>
            {
                var query = new GetProductByIdQuery(productId);
                var result = await bus.InvokeAsync<ErrorOr.ErrorOr<GetProductByIdResponse>>(query, ct);
                return result.ToProblemDetailsOr(r => Results.Ok(r));
            })
        .WithName("GetProductById")
        .WithSummary("Get a product by its ID.")
        .Produces<GetProductByIdResponse>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .AllowAnonymous();
}
