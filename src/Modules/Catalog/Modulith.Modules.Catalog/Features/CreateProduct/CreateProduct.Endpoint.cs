using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modulith.Shared.Infrastructure.Http;
using Wolverine;

namespace Modulith.Modules.Catalog.Features.CreateProduct;

internal static class CreateProductEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost(CatalogRoutes.Products,
            async (CreateProductRequest request, IValidator<CreateProductRequest> validator, IMessageBus bus, CancellationToken ct) =>
            {
                var validation = await validator.ValidateAsync(request, ct);
                if (!validation.IsValid)
                    return Results.ValidationProblem(validation.ToDictionary());

                var command = new CreateProductCommand(request.Sku, request.Name, request.Price, request.Currency);
                var result = await bus.InvokeAsync<ErrorOr.ErrorOr<CreateProductResponse>>(command, ct);
                return result.ToProblemDetailsOr(r => Results.Created($"{CatalogRoutes.Products}/{r.ProductId}", r));
            })
        .WithName("CreateProduct")
        .WithSummary("Create a new product.")
        .Produces<CreateProductResponse>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status409Conflict)
        .RequireAuthorization("Authenticated");
}
