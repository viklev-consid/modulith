using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modulith.Shared.Infrastructure.Http;
using Modulith.Shared.Kernel.Interfaces;
using Wolverine;

namespace Modulith.Modules.Organizations.Features.AcceptOrganizationInvitation;

internal static class AcceptOrganizationInvitationEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost(OrganizationsRoutes.AcceptInvitation,
            async (
                AcceptOrganizationInvitationRequest request,
                HttpContext httpContext,
                ICurrentUser currentUser,
                IMessageBus bus,
                CancellationToken ct) =>
            {
                var email = httpContext.User.FindFirst("email")?.Value
                    ?? httpContext.User.FindFirst(ClaimTypes.Email)?.Value;
                if (currentUser.Id is null || email is null || !Guid.TryParse(currentUser.Id, out var userId))
                {
                    return Results.Unauthorized();
                }

                var result = await bus.InvokeAsync<ErrorOr.ErrorOr<AcceptOrganizationInvitationResponse>>(
                    new AcceptOrganizationInvitationCommand(request.InvitationToken, userId, email),
                    ct);
                return result.ToProblemDetailsOr(Results.Ok);
            })
        .WithName("AcceptOrganizationInvitation")
        .WithSummary("Accept an organization invitation for the current user.")
        .Produces<AcceptOrganizationInvitationResponse>()
        .RequireAuthorization();
}
