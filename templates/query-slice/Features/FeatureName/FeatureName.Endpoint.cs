using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modulith.Modules.ModuleName.Contracts.Authorization;
using Modulith.Shared.Infrastructure.Http;
using Wolverine;

namespace Modulith.Modules.ModuleName.Features.FeatureName;

internal static class FeatureNameEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet($"{ModuleNameRoutes.Prefix}/featurenamelower",
            async (IMessageBus bus, CancellationToken ct) =>
            {
                var query = new FeatureNameQuery();
                var result = await bus.InvokeAsync<ErrorOr.ErrorOr<FeatureNameResponse>>(query, ct);
                return result.ToProblemDetailsOr(r => Results.Ok(r));
            })
        .WithName("FeatureName")
        .WithSummary("FeatureName.")
        .Produces<FeatureNameResponse>(StatusCodes.Status200OK)
        .RequireAuthorization(ModuleNamePermissions.Read);
}
