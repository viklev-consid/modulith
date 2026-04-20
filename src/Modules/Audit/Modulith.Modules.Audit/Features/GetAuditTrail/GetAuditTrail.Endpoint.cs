using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modulith.Modules.Audit.Contracts.Queries;
using Modulith.Shared.Infrastructure.Http;
using Modulith.Shared.Kernel.Interfaces;
using Wolverine;

namespace Modulith.Modules.Audit.Features.GetAuditTrail;

internal static class GetAuditTrailEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet(AuditRoutes.Trail,
            async (
                ICurrentUser currentUser,
                IMessageBus bus,
                CancellationToken ct,
                int page = 1,
                int pageSize = 20) =>
            {
                if (currentUser.Id is null || !Guid.TryParse(currentUser.Id, out var userId))
                    return Results.Unauthorized();

                var query = new GetAuditTrailQuery(userId, page, pageSize);
                var result = await bus.InvokeAsync<ErrorOr.ErrorOr<GetAuditTrailResponse>>(query, ct);
                return result.ToProblemDetailsOr(Results.Ok);
            })
        .WithName("GetAuditTrail")
        .WithSummary("Get the current user's audit trail.")
        .Produces<GetAuditTrailResponse>()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .RequireAuthorization();
}
