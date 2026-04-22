using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace Modulith.Modules.Users.Security.Authorization;

/// <summary>
/// Discovers all <c>*Permissions</c> types in loaded <c>*.Contracts</c> assemblies
/// at construction time, then builds the role→permission map:
/// <list type="bullet">
/// <item><description>admin → every declared permission</description></item>
/// <item><description>user → empty set (endpoints requiring only authentication use the Authenticated policy)</description></item>
/// </list>
/// Additional roles can be added to <see cref="BuildRoleMap"/> without schema changes.
/// </summary>
internal sealed class PermissionCatalog : IPermissionCatalog
{
    private readonly IReadOnlyCollection<string> _allPermissions;
    private readonly Dictionary<string, IReadOnlyCollection<string>> _roleMap;
    private readonly Dictionary<string, string> _versionCache;
    private readonly IReadOnlySet<string> _knownRoles;

    public PermissionCatalog()
    {
        _allPermissions = DiscoverAllPermissions();
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

    private static IReadOnlyCollection<string> DiscoverAllPermissions()
    {
        // Force-load any *.Contracts assemblies that might not yet be loaded
        // (assemblies are loaded lazily; scanning must happen after all modules register).
        EnsureContractsAssembliesLoaded();

        var permissions = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var name = assembly.GetName().Name;
            if (name is null || !name.EndsWith(".Contracts", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var type in assembly.GetExportedTypes())
            {
                if (!type.Name.EndsWith("Permissions", StringComparison.Ordinal) ||
                    !type.IsAbstract || !type.IsSealed)
                {
                    continue;
                }

                var allProp = type.GetProperty(
                    "All",
                    BindingFlags.Public | BindingFlags.Static);

                if (allProp?.GetValue(null) is IEnumerable<string> all)
                {
                    foreach (var p in all)
                    {
                        permissions.Add(p);
                    }
                }
            }
        }

        return [.. permissions];
    }

    /// <summary>
    /// Walks the entry assembly's reference graph and force-loads any
    /// <c>*.Contracts</c> assemblies that haven't been loaded yet.
    /// </summary>
    private static void EnsureContractsAssembliesLoaded()
    {
        var loaded = new HashSet<string>(
            AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetName().Name!)
                .Where(n => n is not null),
            StringComparer.OrdinalIgnoreCase);

        var toVisit = new Queue<AssemblyName>(
            (Assembly.GetEntryAssembly() ?? Assembly.GetCallingAssembly())
            .GetReferencedAssemblies());

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (toVisit.Count > 0)
        {
            var asmName = toVisit.Dequeue();
            var nameStr = asmName.Name;
            if (nameStr is null || !visited.Add(nameStr))
            {
                continue;
            }

            if (!nameStr.EndsWith(".Contracts", StringComparison.Ordinal))
            {
                continue;
            }

            if (!loaded.Contains(nameStr))
            {
                try
                {
                    var loaded2 = Assembly.Load(asmName);
                    foreach (var r in loaded2.GetReferencedAssemblies())
                    {
                        toVisit.Enqueue(r);
                    }
                }
                catch (FileNotFoundException) { /* not in the load context; skip */ }
            }
        }
    }

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
