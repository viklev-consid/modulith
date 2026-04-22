using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modulith.Modules.Audit.Authorization;
using Modulith.Modules.Audit.Contracts.Queries;
using Modulith.Shared.Infrastructure.Authorization;
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
                IResourcePolicy<AuditTrailResource> policy,
                IMessageBus bus,
                CancellationToken ct,
                Guid? actorId = null,
                int page = 1,
                int pageSize = 20) =>
            {
                if (currentUser.Id is null || !Guid.TryParse(currentUser.Id, out var callerId))
                {
                    return Results.Unauthorized();
                }

                // Default to the caller's own trail; admins may pass an explicit actorId.
                var targetId = actorId ?? callerId;

                // Ownership check at the HTTP boundary — keeps the handler pure and
                // callable by internal/background callers without an HTTP user context.
                var resource = new AuditTrailResource(targetId);
                if (!policy.IsAuthorized(currentUser, resource))
                {
                    return Results.Forbid();
                }

                var query = new GetAuditTrailQuery(targetId, page, pageSize);
                var result = await bus.InvokeAsync<ErrorOr.ErrorOr<GetAuditTrailResponse>>(query, ct);
                return result.ToProblemDetailsOr(Results.Ok);
            })
        .WithName("GetAuditTrail")
        .WithSummary("Get an audit trail. Defaults to the caller's own trail; admins may pass actorId to query any user.")
        .Produces<GetAuditTrailResponse>()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .RequireRateLimiting("read");
}
