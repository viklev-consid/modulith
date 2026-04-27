using ErrorOr;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modulith.Shared.Infrastructure.Http;
using Modulith.Shared.Kernel.Interfaces;
using Wolverine;

namespace Modulith.Modules.Users.Features.ExternalLogin.CompleteOnboarding;

internal static class CompleteOnboardingEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost(UsersRoutes.CompleteOnboarding,
            async (
                CompleteOnboardingRequest request,
                [Microsoft.AspNetCore.Mvc.FromServices] IValidator<CompleteOnboardingRequest> validator,
                ICurrentUser currentUser,
                HttpContext httpContext,
                IMessageBus bus,
                CancellationToken ct) =>
            {
                if (currentUser.Id is null || !Guid.TryParse(currentUser.Id, out var userId))
                {
                    return Results.Unauthorized();
                }

                var validation = await validator.ValidateAsync(request, ct);
                if (!validation.IsValid)
                {
                    return Results.ValidationProblem(validation.ToDictionary(), statusCode: StatusCodes.Status422UnprocessableEntity);
                }

                var ip = httpContext.Connection.RemoteIpAddress?.ToString();
                var ua = httpContext.Request.Headers.UserAgent.ToString();
                var command = new CompleteOnboardingCommand(userId, request.AcceptTerms, request.AcceptMarketingEmails, ip, ua);
                var result = await bus.InvokeAsync<ErrorOr<Success>>(command, ct);
                return result.ToProblemDetailsOr(_ => Results.NoContent());
            })
        .WithName("CompleteOnboarding")
        .WithSummary("Accept the terms of service to complete account setup.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .RequireAuthorization()
        .RequireRateLimiting("write");
}
