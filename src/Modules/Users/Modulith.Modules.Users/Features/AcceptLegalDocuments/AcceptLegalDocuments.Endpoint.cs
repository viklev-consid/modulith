using ErrorOr;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modulith.Modules.Users.Domain;
using Modulith.Shared.Infrastructure.Http;
using Modulith.Shared.Kernel.Interfaces;
using Wolverine;

namespace Modulith.Modules.Users.Features.AcceptLegalDocuments;

internal static class AcceptLegalDocumentsEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost(UsersRoutes.LegalAcceptances,
            async (
                AcceptLegalDocumentsRequest request,
                [Microsoft.AspNetCore.Mvc.FromServices] IValidator<AcceptLegalDocumentsRequest> validator,
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

                var command = new AcceptLegalDocumentsCommand(
                    new UserId(userId),
                    request.AcceptedDocuments.Select(d => new AcceptedLegalDocumentCommand(d.DocumentId, d.Version, d.ContentHash)).ToArray(),
                    httpContext.Connection.RemoteIpAddress?.ToString(),
                    httpContext.Request.Headers.UserAgent.ToString());

                var result = await bus.InvokeAsync<ErrorOr<Success>>(command, ct);
                return result.ToProblemDetailsOr(_ => Results.NoContent());
            })
        .WithName("AcceptLegalDocuments")
        .WithSummary("Accept one or more published legal document versions.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .RequireAuthorization()
        .RequireRateLimiting("write");
}
