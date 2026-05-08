using System.Security.Cryptography;
using System.Text;
using Modulith.Shared.Infrastructure.Authorization;

namespace Modulith.Modules.Users.Security.Authorization;

/// <summary>
/// Builds the role→permission map from all registered <see cref="IPermissionSource"/> instances.
/// Each module contributes its permission constants at DI registration time via
/// <c>services.AddPermissions(XxxPermissions.All)</c>; this catalog collects and indexes them.
/// <list type="bullet">
/// <item><description>admin → every permission contributed by any module</description></item>
/// <item><description>user  → empty set</description></item>
/// </list>
/// </summary>
internal sealed class PermissionCatalog : IPermissionCatalog
{
    private readonly IReadOnlyCollection<string> allPermissions;
    private readonly Dictionary<string, IReadOnlyCollection<string>> roleMap;
    private readonly Dictionary<string, string> versionCache;
    private readonly HashSet<string> knownRoles;

    public PermissionCatalog(IEnumerable<IPermissionSource> sources)
    {
        allPermissions = [.. sources
            .SelectMany(s => s.GetPermissions())
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];

        roleMap = BuildRoleMap(allPermissions);
        versionCache = BuildVersionCache(roleMap);
        knownRoles = new HashSet<string>(roleMap.Keys, StringComparer.OrdinalIgnoreCase);
        // Note: knownRoles is typed as HashSet<string> so ResolveRole can call TryGetValue,
        // which returns the stored canonical key rather than just true/false.
    }

    public IReadOnlyCollection<string> AllPermissions => allPermissions;

    public IReadOnlySet<string> KnownRoles => knownRoles;

    public string? ResolveRole(string name) =>
        knownRoles.TryGetValue(name, out var canonical) ? canonical : null;

    public IReadOnlyCollection<string> GetPermissionsForRole(string roleName) =>
        roleMap.TryGetValue(roleName, out var perms) ? perms : [];

    public string GetPermissionsVersion(string roleName) =>
        versionCache.TryGetValue(roleName, out var v) ? v : ComputeVersion([]);

    // ---------- private helpers ----------

    private static Dictionary<string, IReadOnlyCollection<string>> BuildRoleMap(
        IReadOnlyCollection<string> all)
    {
        return new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["admin"] = all,
            ["user"] = []
        };
    }

    private static Dictionary<string, string> BuildVersionCache(
        Dictionary<string, IReadOnlyCollection<string>> roleMap)
    {
        var cache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (role, perms) in roleMap)
        {
            cache[role] = ComputeVersion(perms);
        }
        return cache;
    }

    private static string ComputeVersion(IEnumerable<string> permissions)
    {
        var joined = string.Join('\n', permissions.Order(StringComparer.Ordinal));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(joined));
        return Convert.ToBase64String(hash)[..16]
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
