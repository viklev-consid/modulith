using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modulith.Modules.Users.Contracts.Authorization;
using Modulith.Shared.Infrastructure.Http;
using Wolverine;

namespace Modulith.Modules.Users.Features.ListUsers;

internal static class ListUsersEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet(UsersRoutes.List,
            async (int page, int pageSize, IMessageBus bus, CancellationToken ct) =>
            {
                var query = new ListUsersQuery(page, pageSize);
                var result = await bus.InvokeAsync<ErrorOr.ErrorOr<ListUsersResponse>>(query, ct);
                return result.ToProblemDetailsOr(Results.Ok);
            })
        .WithName("ListUsers")
        .WithSummary("List all users. Requires users.users.read permission.")
        .Produces<ListUsersResponse>()
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .RequireAuthorization(UsersPermissions.UsersRead)
        .RequireRateLimiting("read");
}
