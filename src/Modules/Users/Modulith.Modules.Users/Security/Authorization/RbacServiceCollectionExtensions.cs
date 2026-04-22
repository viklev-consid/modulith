using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Modulith.Modules.Users.Security.Authorization;

public static class RbacServiceCollectionExtensions
{
    /// <summary>
    /// Registers the RBAC infrastructure:
    /// <list type="bullet">
    ///   <item><description><see cref="IPermissionCatalog"/> — collects permissions from all registered <c>IPermissionSource</c> instances and builds the role→permissions map</description></item>
    ///   <item><description><see cref="PermissionClaimsTransformation"/> — injects permission claims per request</description></item>
    ///   <item><description><see cref="PermissionAuthorizationHandler"/> — evaluates permission requirements</description></item>
    ///   <item><description>One named <see cref="AuthorizationPolicy"/> per declared permission constant</description></item>
    /// </list>
    /// Each module contributes its permissions by calling <c>services.AddPermissions(XxxPermissions.All)</c>
    /// in its own <c>Add*Module</c> extension. <c>AddRbac</c> can be called at any point after those
    /// registrations; there is no load-order constraint.
    /// </summary>
    public static IServiceCollection AddRbac(this IServiceCollection services)
    {
        // Singleton: catalog is computed once at startup from the loaded assemblies.
        services.AddSingleton<IPermissionCatalog, PermissionCatalog>();

        // IClaimsTransformation is called per request by the auth middleware.
        services.AddScoped<IClaimsTransformation, PermissionClaimsTransformation>();

        // Authorization handler must be registered so the policy evaluation engine finds it.
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

        // Register one named policy per declared permission. Endpoints call
        // .RequireAuthorization(CatalogPermissions.ProductsWrite) using these names.
        services.AddOptions<AuthorizationOptions>()
            .Configure<IPermissionCatalog>((opts, catalog) =>
            {
                foreach (var permission in catalog.AllPermissions)
                {
                    opts.AddPolicy(permission, policy =>
                    {
                        policy.RequireAuthenticatedUser();
                        policy.AddRequirements(new PermissionRequirement(permission));
                    });
                }
            });

        return services;
    }
}
