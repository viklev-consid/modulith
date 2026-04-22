using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modulith.Modules.Users.Contracts.Authorization;
using Modulith.Modules.Users.Domain;
using Modulith.Shared.Infrastructure.Http;
using Wolverine;

namespace Modulith.Modules.Users.Features.GetUserById;

internal static class GetUserByIdEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet(UsersRoutes.ById,
            async (Guid userId, IMessageBus bus, CancellationToken ct) =>
            {
                var query = new GetUserByIdQuery(new UserId(userId));
                var result = await bus.InvokeAsync<ErrorOr.ErrorOr<GetUserByIdResponse>>(query, ct);
                return result.ToProblemDetailsOr(Results.Ok);
            })
        .WithName("GetUserById")
        .WithSummary("Get a specific user by ID. Requires users.users.read permission.")
        .Produces<GetUserByIdResponse>()
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .RequireAuthorization(UsersPermissions.UsersRead)
        .RequireRateLimiting("read");
}
