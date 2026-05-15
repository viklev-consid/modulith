using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modulith.Shared.Infrastructure.Http;
using Modulith.Shared.Kernel.Interfaces;
using Wolverine;

namespace Modulith.Modules.Users.Features.UpdateProfile;

internal static class UpdateProfileEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPatch(UsersRoutes.Profile,
            async (
                UpdateProfileRequest request,
                [Microsoft.AspNetCore.Mvc.FromServices] IValidator<UpdateProfileRequest> validator,
                [Microsoft.AspNetCore.Mvc.FromServices] ICurrentUser currentUser,
                IMessageBus bus,
                CancellationToken ct) =>
            {
                var validation = await validator.ValidateAsync(request, ct);
                if (!validation.IsValid)
                {
                    return Results.ValidationProblem(validation.ToDictionary(), statusCode: StatusCodes.Status422UnprocessableEntity);
                }

                if (!Guid.TryParse(currentUser.Id, out var userId))
                {
                    return Results.Unauthorized();
                }

                var command = new UpdateProfileCommand(userId, request.DisplayName);
                var result = await bus.InvokeAsync<ErrorOr.ErrorOr<UpdateProfileResponse>>(command, ct);
                return result.ToProblemDetailsOr(Results.Ok);
            })
        .WithName("UpdateProfile")
        .WithSummary("Update the authenticated user's profile.")
        .Produces<UpdateProfileResponse>()
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .RequireAuthorization()
        .RequireRateLimiting("write");
}
