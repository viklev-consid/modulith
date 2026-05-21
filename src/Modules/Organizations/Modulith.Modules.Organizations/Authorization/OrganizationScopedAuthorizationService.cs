using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Organizations.Contracts.Authorization;
using Modulith.Modules.Organizations.Domain;
using Modulith.Modules.Organizations.Persistence;
using Modulith.Shared.Infrastructure.Authorization;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Organizations.Authorization;

internal sealed class OrganizationScopedAuthorizationService(OrganizationsDbContext db)
    : IScopedAuthorizationService<OrganizationScope>
{
    public async Task<ScopedAuthorizationResult> AuthorizeAsync(
        ICurrentUser currentUser,
        OrganizationScope scope,
        string permission,
        ScopedAuthorizationOptions options,
        CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || currentUser.Id is null)
        {
            return ScopedAuthorizationResult.Denied;
        }

        if (options.AllowPlatformOverride &&
            currentUser.HasPermission(OrganizationsPermissions.PlatformOverride))
        {
            return ScopedAuthorizationResult.PlatformOverride;
        }

        if (!Guid.TryParse(currentUser.Id, out var userId))
        {
            return ScopedAuthorizationResult.Denied;
        }

        var organizationId = new OrganizationId(scope.OrganizationId);
        var membership = await db.Memberships
            .AsNoTracking()
            .Where(m => m.OrganizationId == organizationId && m.UserId == userId && m.IsActive)
            .Select(m => new { m.Role })
            .FirstOrDefaultAsync(ct);

        if (membership is null)
        {
            return ScopedAuthorizationResult.Denied;
        }

        return OrganizationRolePermissionMap.GetPermissions(membership.Role).Contains(permission, StringComparer.Ordinal)
            ? ScopedAuthorizationResult.ScopedPermission
            : ScopedAuthorizationResult.Denied;
    }
}
