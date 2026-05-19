using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Modules.Users.Domain;

namespace Modulith.Modules.Users.Legal;

internal static class UsersLegalComplianceMiddleware
{
    public static IApplicationBuilder UseUsersLegalCompliance(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            if (ShouldSkip(context) ||
                context.User.Identity?.IsAuthenticated != true ||
                !Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
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

            context.Response.StatusCode = StatusCodes.Status428PreconditionRequired;
            await context.Response.WriteAsJsonAsync(new
            {
                code = "Users.LegalAcceptance.Required",
                detail = "Updated legal terms must be accepted before continuing.",
            }, context.RequestAborted);
        });

    private static bool ShouldSkip(HttpContext context)
    {
        var path = context.Request.Path;
        var method = context.Request.Method;

        if (!path.StartsWithSegments(UsersRoutes.Me, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (HttpMethods.IsGet(method) && string.Equals(path.Value, UsersRoutes.Me, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return path.StartsWithSegments(UsersRoutes.LegalCompliance, StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments(UsersRoutes.LegalAcceptances, StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments(UsersRoutes.OnboardingLegalRequirements, StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments(UsersRoutes.CompleteOnboarding, StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments(UsersRoutes.Logout, StringComparison.OrdinalIgnoreCase);
    }
}
