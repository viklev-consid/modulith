using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Modulith.Modules.Users.Security.Authorization;

public static class RbacServiceCollectionExtensions
{
    /// <summary>
    /// Registers the RBAC infrastructure:
    /// <list type="bullet">
    ///   <item><description><see cref="IPermissionCatalog"/> — discovers and maps role→permissions</description></item>
    ///   <item><description><see cref="PermissionClaimsTransformation"/> — injects permission claims per request</description></item>
    ///   <item><description><see cref="PermissionAuthorizationHandler"/> — evaluates permission requirements</description></item>
    ///   <item><description>One named <see cref="AuthorizationPolicy"/> per declared permission constant</description></item>
    /// </list>
    /// Must be called <em>after</em> all module <c>AddXxxModule</c> registrations so that
    /// every <c>*.Contracts</c> assembly is already loaded when the catalog scans them.
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
