using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

namespace Modulith.Modules.Users.Security.Authorization;

/// <summary>
/// On each authenticated request, reads the <c>role</c> claim, resolves the matching
/// permission set from <see cref="IPermissionCatalog"/>, and adds <c>permission</c>
/// claims to a cloned identity. Idempotent — skips if the <c>permission</c> claims
/// are already present.
/// </summary>
internal sealed class PermissionClaimsTransformation(IPermissionCatalog catalog)
    : IClaimsTransformation
{
    private const string PermissionClaimType = "permission";

    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        // Idempotent: skip if permission claims already present.
        if (principal.HasClaim(c => c.Type == PermissionClaimType))
        {
            return Task.FromResult(principal);
        }

        var roleName = principal.FindFirstValue(ClaimTypes.Role);
        if (roleName is null)
        {
            return Task.FromResult(principal);
        }

        var permissions = catalog.GetPermissionsForRole(roleName);
        if (permissions.Count == 0)
        {
            return Task.FromResult(principal);
        }

        var clonedIdentity = new ClaimsIdentity(principal.Identity);
        foreach (var permission in permissions)
        {
            clonedIdentity.AddClaim(new Claim(PermissionClaimType, permission));
        }

        var clonedPrincipal = new ClaimsPrincipal(clonedIdentity);
        // Copy over any other identities the principal may have.
        foreach (var identity in principal.Identities.Skip(1))
        {
            clonedPrincipal.AddIdentity(identity);
        }

        return Task.FromResult(clonedPrincipal);
    }
}
