using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modulith.Shared.Infrastructure.Http;
using Wolverine;

namespace Modulith.Modules.ModuleName.Features.FeatureName;

internal static class FeatureNameEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/v1/modulenamelower/featurenamelower",
            async (
                FeatureNameRequest request,
                [Microsoft.AspNetCore.Mvc.FromServices] IValidator<FeatureNameRequest> validator,
                IMessageBus bus,
                CancellationToken ct) =>
            {
                var validation = await validator.ValidateAsync(request, ct);
                if (!validation.IsValid)
                    return Results.ValidationProblem(
                        validation.ToDictionary(),
                        statusCode: StatusCodes.Status422UnprocessableEntity);

                var command = new FeatureNameCommand();
                var result = await bus.InvokeAsync<ErrorOr.ErrorOr<FeatureNameResponse>>(command, ct);
                return result.ToProblemDetailsOr(r => Results.Ok(r));
            })
        .WithName("FeatureName")
        .WithSummary("FeatureName.")
        .Produces<FeatureNameResponse>(StatusCodes.Status200OK)
        .ProducesValidationProblem()
        .RequireAuthorization("Authenticated");
}
