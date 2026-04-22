using Microsoft.AspNetCore.Authorization;

namespace Modulith.Modules.Users.Security.Authorization;

internal sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}
