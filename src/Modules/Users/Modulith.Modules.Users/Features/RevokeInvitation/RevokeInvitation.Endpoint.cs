using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modulith.Modules.Users.Contracts.Authorization;
using Modulith.Shared.Infrastructure.Http;
using Wolverine;

namespace Modulith.Modules.Users.Features.RevokeInvitation;

internal static class RevokeInvitationEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapDelete(UsersRoutes.InvitationById,
            async (Guid invitationId, IMessageBus bus, CancellationToken ct) =>
            {
                var command = new RevokeInvitationCommand(invitationId);
                var result = await bus.InvokeAsync<ErrorOr.ErrorOr<RevokeInvitationResponse>>(command, ct);
                return result.ToProblemDetailsOr(Results.Ok);
            })
        .WithName("RevokeInvitation")
        .WithSummary("Revoke a pending user invitation. Admin only.")
        .Produces<RevokeInvitationResponse>()
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .RequireAuthorization(UsersPermissions.InvitationsWrite)
        .RequireRateLimiting("write");
}
