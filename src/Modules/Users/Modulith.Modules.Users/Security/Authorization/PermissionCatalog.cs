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
    private readonly IReadOnlyCollection<string> _allPermissions;
    private readonly Dictionary<string, IReadOnlyCollection<string>> _roleMap;
    private readonly Dictionary<string, string> _versionCache;
    private readonly IReadOnlySet<string> _knownRoles;

    public PermissionCatalog(IEnumerable<IPermissionSource> sources)
    {
        _allPermissions = [.. sources
            .SelectMany(s => s.GetPermissions())
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];

        _roleMap = BuildRoleMap(_allPermissions);
        _versionCache = BuildVersionCache(_roleMap);
        _knownRoles = new HashSet<string>(_roleMap.Keys, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<string> AllPermissions => _allPermissions;

    public IReadOnlySet<string> KnownRoles => _knownRoles;

    public IReadOnlyCollection<string> GetPermissionsForRole(string roleName) =>
        _roleMap.TryGetValue(roleName, out var perms) ? perms : [];

    public string GetPermissionsVersion(string roleName) =>
        _versionCache.TryGetValue(roleName, out var v) ? v : ComputeVersion([]);

    // ---------- private helpers ----------

    private static Dictionary<string, IReadOnlyCollection<string>> BuildRoleMap(
        IReadOnlyCollection<string> all)
    {
        return new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["admin"] = all,
            ["user"]  = []
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
