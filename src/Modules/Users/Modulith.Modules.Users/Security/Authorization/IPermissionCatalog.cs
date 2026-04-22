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

    /// <summary>
    /// The set of role names that have explicit permission mappings. Only roles
    /// in this set may be assigned to users. Role names are case-insensitive.
    /// </summary>
    IReadOnlySet<string> KnownRoles { get; }

    /// <summary>
    /// Returns the canonical (lowercase) role name if <paramref name="name"/> matches a known
    /// role (case-insensitive), or <c>null</c> if the role is unknown.
    /// Use this instead of <see cref="KnownRoles"/>.<c>Contains</c> whenever you need to
    /// persist or compare the name, to guarantee canonical casing regardless of caller input.
    /// </summary>
    string? ResolveRole(string name);
}
