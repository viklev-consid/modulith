using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modulith.Shared.Infrastructure.Http;
using Wolverine;

namespace Modulith.Modules.Users.Features.GetOnboardingLegalRequirements;

internal static class GetOnboardingLegalRequirementsEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet(UsersRoutes.OnboardingLegalRequirements,
            async (IMessageBus bus, HttpContext http, CancellationToken ct) =>
            {
                http.Response.Headers.CacheControl = "private, no-store";
                var result = await bus.InvokeAsync<ErrorOr.ErrorOr<GetOnboardingLegalRequirementsResponse>>(
                    new GetOnboardingLegalRequirementsQuery(),
                    ct);

                return result.ToProblemDetailsOr(Results.Ok);
            })
        .WithName("GetOnboardingLegalRequirements")
        .WithSummary("Get the current legal documents required to complete onboarding.")
        .Produces<GetOnboardingLegalRequirementsResponse>()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .RequireAuthorization();
}
