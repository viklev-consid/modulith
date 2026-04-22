namespace Modulith.Modules.Users.Security.Authorization;

/// <summary>
/// Resolves the set of permissions for a named role, and provides the full
/// catalog of all permissions declared across all loaded modules.
/// </summary>
public interface IPermissionCatalog
{
    /// <summary>Returns all permissions granted to the specified role name.</summary>
    IReadOnlyCollection<string> GetPermissionsForRole(string roleName);

    /// <summary>Returns every permission constant declared across all modules.</summary>
    IReadOnlyCollection<string> AllPermissions { get; }

    /// <summary>
    /// Returns a stable short hash of the sorted permission list for the given role.
    /// Cached per role; changes only on process restart / code change.
    /// </summary>
    string GetPermissionsVersion(string roleName);
}
