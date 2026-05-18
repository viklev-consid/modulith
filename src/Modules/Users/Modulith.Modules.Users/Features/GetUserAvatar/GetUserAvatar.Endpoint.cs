using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modulith.Shared.Infrastructure.Http;
using Modulith.Shared.Kernel.Interfaces;
using Wolverine;

namespace Modulith.Modules.Users.Features.GetUserAvatar;

internal static class GetUserAvatarEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet(UsersRoutes.UserAvatar,
            async (
                Guid userId,
                ICurrentUser currentUser,
                IMessageBus bus,
                HttpContext http,
                CancellationToken ct) =>
            {
                if (!Guid.TryParse(currentUser.Id, out var requestingUserId))
                {
                    return Results.Unauthorized();
                }

                var result = await bus.InvokeAsync<ErrorOr.ErrorOr<GetUserAvatarResponse>>(
                    new GetUserAvatarQuery(userId, requestingUserId, currentUser.Role, http.Request.Headers.IfNoneMatch.ToString()),
                    ct);

                return result.Match<IResult>(
                    avatar =>
                    {
                        http.Response.Headers.CacheControl = "private, max-age=300, must-revalidate";
                        http.Response.Headers.ETag = avatar.ETag;
                        return avatar.NotModified
                            ? Results.StatusCode(StatusCodes.Status304NotModified)
                            : Results.Stream(avatar.Content!, avatar.ContentType!);
                    },
                    Problems.FromErrors);
            })
        .WithName("GetUserAvatar")
        .WithSummary("Get a user's avatar when the authenticated caller is allowed to view it.")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .RequireAuthorization();
}
