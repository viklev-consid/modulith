using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modulith.Shared.Infrastructure.Http;
using Modulith.Shared.Kernel.Interfaces;
using Wolverine;

namespace Modulith.Modules.Users.Features.RequestEmailChange;

internal static class RequestEmailChangeEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost(UsersRoutes.RequestEmailChange,
            async (
                RequestEmailChangeRequest request,
                [Microsoft.AspNetCore.Mvc.FromServices] IValidator<RequestEmailChangeRequest> validator,
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

                var command = new RequestEmailChangeCommand(userId, request.NewEmail, request.CurrentPassword);
                var result = await bus.InvokeAsync<ErrorOr.ErrorOr<RequestEmailChangeResponse>>(command, ct);
                return result.ToProblemDetailsOr(Results.Ok);
            })
        .WithName("RequestEmailChange")
        .WithSummary("Request an email address change. A confirmation link will be sent to the new address.")
        .Produces<RequestEmailChangeResponse>()
        .ProducesValidationProblem()
        .RequireAuthorization()
        .RequireRateLimiting("write");
}
