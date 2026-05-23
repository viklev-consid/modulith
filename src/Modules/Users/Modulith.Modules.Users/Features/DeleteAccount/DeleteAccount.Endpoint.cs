using ErrorOr;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modulith.Modules.Users.Domain;
using Modulith.Shared.Infrastructure.Http;
using Modulith.Shared.Kernel.Interfaces;
using Wolverine;

namespace Modulith.Modules.Users.Features.DeleteAccount;

internal static class DeleteAccountEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapDelete(UsersRoutes.Me,
            async (ICurrentUser currentUser, IMessageBus bus, CancellationToken ct) =>
            {
                if (currentUser.Id is null || !Guid.TryParse(currentUser.Id, out var userId))
                {
                    return Results.Unauthorized();
                }

                var command = new DeleteAccountCommand(new UserId(userId));
                var result = await bus.InvokeAsync<ErrorOr.ErrorOr<DeleteAccountResponse>>(command, ct);
                return result.ToProblemDetailsOr(response =>
                    response.CanDelete
                        ? Results.NoContent()
                        : Results.Problem(
                            title: "Conflict",
                            detail: "Transfer ownership or delete owned organizations before deleting this user.",
                            statusCode: StatusCodes.Status409Conflict,
                            extensions: new Dictionary<string, object?>(StringComparer.Ordinal)
                            {
                                ["errorCode"] = "Organizations.Owner.UserErasureBlocked",
                                ["blockingOrganizations"] = response.BlockingOrganizations
                            }));
            })
        .WithName("DeleteAccount")
        .WithSummary("Permanently delete the authenticated user's account and all associated personal data.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .RequireAuthorization();
}
