using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Modules.Users.Domain;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Users.Legal;

internal static class UsersLegalComplianceMiddleware
{
    public static IApplicationBuilder UseUsersLegalCompliance(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var currentUser = context.RequestServices.GetRequiredService<ICurrentUser>();
            if (ShouldSkip(context) ||
                !currentUser.IsAuthenticated ||
                !Guid.TryParse(currentUser.Id, out var userId))
            {
                await next(context);
                return;
            }

            var complianceService = context.RequestServices.GetRequiredService<ILegalComplianceService>();
            var compliance = await complianceService.GetContinuedUseComplianceAsync(new UserId(userId), context.RequestAborted);

            if (compliance.BlockingLevel != LegalDocumentBlockingLevel.BlockAllAuthenticatedUse)
            {
                await next(context);
                return;
            }

            var result = Results.Problem(
                type: "https://developer.mozilla.org/docs/Web/HTTP/Status/428",
                title: "Legal acceptance required",
                detail: "Updated legal terms must be accepted before continuing.",
                statusCode: StatusCodes.Status428PreconditionRequired,
                extensions: new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["errorCode"] = "Users.LegalAcceptance.Required",
                    ["traceId"] = Activity.Current?.TraceId.ToString(),
                    ["missingDocuments"] = compliance.MissingDocuments.Select(document => new
                    {
                        documentId = document.Id.Value,
                        type = LegalDocumentMapper.ToWireType(document.DocumentType),
                        document.Version,
                        document.ContentHash,
                    }).ToArray(),
                });

            await result.ExecuteAsync(context);
        });

    private static bool ShouldSkip(HttpContext context)
    {
        var path = context.Request.Path;
        var method = context.Request.Method;

        if (!path.StartsWithSegments("/v1", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return path.StartsWithSegments(UsersRoutes.LegalCompliance, StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments(UsersRoutes.LegalAcceptances, StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments(UsersRoutes.OnboardingLegalRequirements, StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments(UsersRoutes.CompleteOnboarding, StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments(UsersRoutes.Logout, StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments(UsersRoutes.PersonalData, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(path.Value, UsersRoutes.Me, StringComparison.OrdinalIgnoreCase) &&
                HttpMethods.IsDelete(method);
    }
}
