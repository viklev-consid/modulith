using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modulith.Modules.Users.Domain;
using Modulith.Shared.Infrastructure.Http;
using Modulith.Shared.Kernel.Interfaces;
using Wolverine;

namespace Modulith.Modules.Users.Features.GetLegalCompliance;

internal static class GetLegalComplianceEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet(UsersRoutes.LegalCompliance,
            async (ICurrentUser currentUser, IMessageBus bus, HttpContext http, CancellationToken ct) =>
            {
                if (currentUser.Id is null || !Guid.TryParse(currentUser.Id, out var userId))
                {
                    return Results.Unauthorized();
                }

                http.Response.Headers.CacheControl = "private, no-store";
                var result = await bus.InvokeAsync<ErrorOr.ErrorOr<GetLegalComplianceResponse>>(
                    new GetLegalComplianceQuery(new UserId(userId)),
                    ct);

                return result.ToProblemDetailsOr(Results.Ok);
            })
        .WithName("GetLegalCompliance")
        .WithSummary("Get the authenticated user's current legal document compliance status.")
        .Produces<GetLegalComplianceResponse>()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .RequireAuthorization();
}
