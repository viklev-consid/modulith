using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modulith.Modules.Users.Errors;
using Modulith.Modules.Users.Legal;
using Modulith.Shared.Infrastructure.Http;
using Wolverine;

namespace Modulith.Modules.Users.Features.GetLegalDocument;

internal static class GetLegalDocumentEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet(UsersRoutes.LegalDocumentByTypeAndVersion,
            async (string type, string version, IMessageBus bus, HttpContext http, CancellationToken ct) =>
            {
                if (!LegalDocumentMapper.TryFromWireType(type, out var documentType))
                {
                    return NotFound().ToProblemDetailsOr(Results.Ok);
                }

                var result = await bus.InvokeAsync<ErrorOr.ErrorOr<GetLegalDocumentResponse>>(
                    new GetLegalDocumentQuery(documentType, version),
                    ct);

                return result.ToProblemDetailsOr(response =>
                {
                    http.Response.Headers.CacheControl = "private, max-age=300";
                    return Results.Ok(response);
                });
            })
        .WithName("GetLegalDocument")
        .WithSummary("Get the current published content for a legal document version.")
        .Produces<GetLegalDocumentResponse>()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .RequireAuthorization();

    private static ErrorOr.ErrorOr<GetLegalDocumentResponse> NotFound() =>
        UsersErrors.LegalDocumentNotFound;
}
