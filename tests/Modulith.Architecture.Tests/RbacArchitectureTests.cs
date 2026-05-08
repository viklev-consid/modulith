using System.Reflection;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Modulith.Shared.Infrastructure.Identity;

namespace Modulith.Architecture.Tests;

/// <summary>
/// Architectural rules specific to the RBAC system. See ADR-0030.
/// </summary>
[Trait("Category", "Architecture")]
public sealed class RbacArchitectureTests
{
    private static readonly IReadOnlyList<Assembly> allContractsAssemblies =
        ModuleAssemblyCatalog.ContractsAssemblies;

    // All module assemblies that contain Endpoint types.
    private static readonly IReadOnlyList<Assembly> allModuleAssemblies =
        ModuleAssemblyCatalog.ModuleAssemblies;

    private static readonly Regex permissionFormat =
        new(@"^[a-z][a-z0-9_-]*\.[a-z][a-z0-9_-]*\.[a-z][a-z0-9_-]*$",
            RegexOptions.NonBacktracking | RegexOptions.CultureInvariant);

    [Fact]
    public void PermissionConstants_MustFollowNamingConvention()
    {
        // Permission constants must follow the format: {module}.{resource}.{action}
        // All parts lowercase, dot-separated. See ADR-0030.
        var violations = new List<string>();

        foreach (var assembly in allContractsAssemblies)
        {
            var permissionTypes = assembly.GetExportedTypes()
                .Where(t => t.Name.EndsWith("Permissions", StringComparison.Ordinal)
                         && t.Namespace?.Contains(".Authorization") == true);

            foreach (var type in permissionTypes)
            {
                var constants = type.GetFields(BindingFlags.Public | BindingFlags.Static)
                    .Where(f => f.IsLiteral && f.FieldType == typeof(string))
                    .Select(f => (Name: $"{type.FullName}.{f.Name}", Value: (string)f.GetRawConstantValue()!));

                foreach (var (name, value) in constants)
                {
                    if (!permissionFormat.IsMatch(value))
                    {
                        violations.Add($"{name} = \"{value}\"");
                    }
                }
            }
        }

        Assert.True(violations.Count == 0,
            "FAIL: Permission constants must follow the format '{module}.{resource}.{action}' " +
            "(all lowercase, dot-separated, 3 segments). See ADR-0030. " +
            $"Offending constants: {string.Join(", ", violations)}");
    }

    [Fact]
    public void PermissionTypes_MustResideInContractsAuthorizationNamespace()
    {
        // *Permissions types must live in *.Contracts/Authorization/ to ensure they are
        // accessible to consuming modules without crossing internal boundaries. See ADR-0030.
        var violations = new List<string>();

        foreach (var assembly in allContractsAssemblies)
        {
            var badTypes = assembly.GetExportedTypes()
                .Where(t => t.Name.EndsWith("Permissions", StringComparison.Ordinal))
                .Where(t => t.Namespace?.Contains(".Authorization") != true)
                .Select(t => t.FullName!);

            violations.AddRange(badTypes);
        }

        Assert.True(violations.Count == 0,
            "FAIL: *Permissions types must reside in a namespace containing '.Authorization' " +
            "within a *.Contracts project. See ADR-0030. " +
            $"Offending types: {string.Join(", ", violations)}");
    }

    [Fact]
    public void ClaimsTypeRole_MustOnlyBeReadInCurrentUser()
    {
        // ClaimTypes.Role must only be referenced in the CurrentUser implementation.
        // Handlers and endpoints must read role/permissions via ICurrentUser. See ADR-0030.

        var violations = allModuleAssemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => !string.Equals(t.Name, nameof(CurrentUser), StringComparison.Ordinal))
            .Where(t => t.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Static | BindingFlags.Instance |
                BindingFlags.DeclaredOnly)
                .Any(m => ReferencesClaimsTypeRole(m)))
            .Select(t => t.FullName!)
            .ToList();

        // Also check the shared infrastructure assembly (except CurrentUser itself).
        var sharedViolations = typeof(CurrentUser).Assembly
            .GetTypes()
            .Where(t => !string.Equals(t.Name, nameof(CurrentUser), StringComparison.Ordinal))
            .Where(t => t.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Static | BindingFlags.Instance |
                BindingFlags.DeclaredOnly)
                .Any(m => ReferencesClaimsTypeRole(m)))
            .Select(t => t.FullName!);

        violations.AddRange(sharedViolations);

        Assert.True(violations.Count == 0,
            "FAIL: ClaimTypes.Role must only be referenced in CurrentUser. " +
            "Use ICurrentUser.Role or ICurrentUser.HasPermission() instead. See ADR-0030. " +
            $"Offending types: {string.Join(", ", violations)}");
    }

    private static bool ReferencesClaimsTypeRole(MethodInfo method)
    {
        try
        {
            var body = method.GetMethodBody();
            if (body is null)
            {
                return false;
            }

            var il = body.GetILAsByteArray();
            if (il is null)
            {
                return false;
            }

            var mod = method.Module;
            for (var i = 0; i < il.Length - 4; i++)
            {
                // ldsfld opcode (0x7E) is used to load static fields like ClaimTypes.Role
                if (il[i] == 0x7E)
                {
                    var token = BitConverter.ToInt32(il, i + 1);
                    try
                    {
                        var field = mod.ResolveField(token);
                        if (field?.DeclaringType == typeof(ClaimTypes)
                            && string.Equals(field.Name, nameof(ClaimTypes.Role), StringComparison.Ordinal))
                        {
                            return true;
                        }
                    }
                    catch { /* unresolvable */ }
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }
}
