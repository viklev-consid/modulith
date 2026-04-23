using Microsoft.Extensions.DependencyInjection;

namespace Modulith.Shared.Infrastructure.Authorization;

/// <summary>
/// Extension to register a module's permission constants with the catalog.
/// </summary>
public static class PermissionSourceExtensions
{
    /// <summary>
    /// Registers <paramref name="permissions"/> as a permission source so the
    /// <c>PermissionCatalog</c> includes them in the admin role and named authorization policies.
    /// Call this once per module in <c>Add*Module</c>, before <c>AddRbac()</c>.
    /// </summary>
    public static IServiceCollection AddPermissions(
        this IServiceCollection services,
        IReadOnlyCollection<string> permissions)
    {
        services.AddSingleton<IPermissionSource>(new StaticPermissionSource(permissions));
        return services;
    }

    private sealed class StaticPermissionSource(IReadOnlyCollection<string> permissions) : IPermissionSource
    {
        public IReadOnlyCollection<string> GetPermissions() => permissions;
    }
}
