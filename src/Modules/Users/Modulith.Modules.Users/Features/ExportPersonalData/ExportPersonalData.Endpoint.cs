using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modulith.Modules.Users.Domain;
using Modulith.Shared.Infrastructure.Http;
using Modulith.Shared.Kernel.Interfaces;
using Wolverine;

namespace Modulith.Modules.Users.Features.ExportPersonalData;

internal static class ExportPersonalDataEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet(UsersRoutes.PersonalData,
            async (ICurrentUser currentUser, IMessageBus bus, CancellationToken ct) =>
            {
                if (currentUser.Id is null || !Guid.TryParse(currentUser.Id, out var userId))
                    return Results.Unauthorized();

                var query = new ExportPersonalDataQuery(new UserId(userId));
                var result = await bus.InvokeAsync<ErrorOr.ErrorOr<ExportPersonalDataResponse>>(query, ct);
                return result.ToProblemDetailsOr(Results.Ok);
            })
        .WithName("ExportPersonalData")
        .WithSummary("Export all personal data held about the authenticated user.")
        .Produces<ExportPersonalDataResponse>()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .RequireAuthorization();
}
