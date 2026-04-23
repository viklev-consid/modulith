using System.Reflection;
using Modulith.Modules.Audit.Contracts.Queries;
using Modulith.Modules.Audit.Domain;
using Modulith.Modules.Catalog.Contracts.Events;
using Modulith.Modules.Catalog.Domain;
using Modulith.Modules.Notifications.Domain;
using Modulith.Modules.Users.Contracts.Events;
using Modulith.Modules.Users.Domain;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Architecture.Tests;

/// <summary>
/// Cross-cutting architectural rules that apply to all modules collectively.
/// Covers IConfiguration injection discipline, integration event shape, GDPR coverage,
/// and blob storage isolation. See ADR-0015 for the full rule catalogue.
/// </summary>
[Trait("Category", "Architecture")]
public sealed class GeneralArchitectureTests
{
    private static readonly Assembly[] AllModuleAssemblies =
    [
        typeof(User).Assembly,
        typeof(Product).Assembly,
        typeof(AuditEntry).Assembly,
        typeof(NotificationLog).Assembly,
    ];

    private static readonly Assembly[] AllContractsAssemblies =
    [
        typeof(UserRegisteredV1).Assembly,
        typeof(ProductCreatedV1).Assembly,
        typeof(GetAuditTrailQuery).Assembly,
    ];

    [Fact]
    public void IConfiguration_MustOnlyBeInjectedInModuleRegistrationTypes()
    {
        // IConfiguration may only appear as a constructor parameter in types whose name ends in "Module".
        // All other types must use strongly-typed IOptions<T> with ValidateOnStart(). See ADR-0021.
        var iConfigType = typeof(Microsoft.Extensions.Configuration.IConfiguration);

        var violations = AllModuleAssemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => !t.Name.EndsWith("Module", StringComparison.Ordinal))
            .Where(t => t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Any(ctor => ctor.GetParameters()
                    .Any(p => p.ParameterType == iConfigType
                           || p.ParameterType.IsAssignableTo(iConfigType))))
            .Select(t => t.FullName!)
            .ToList();

        Assert.True(violations.Count == 0,
            "FAIL: IConfiguration must only be injected in types whose name ends in 'Module' " +
            "(i.e., the module registration extensions). " +
            "Other types must use IOptions<T> bound in the Module registration. " +
            "See ADR-0021 (Configuration and Secrets). " +
            $"Offending types: {string.Join(", ", violations)}");
    }

    [Fact]
    public void IntegrationEvents_MustBeSealedRecords()
    {
        // Integration events in *.Contracts.Events.* must be declared as 'sealed record'.
        // 'sealed' prevents serialization surprises from subclassing
        // 'record' provides value equality and immutability suitable for message envelopes.
        var violations = AllContractsAssemblies
            .SelectMany(a => a.GetExportedTypes())
            .Where(t => t.Namespace?.Contains(".Events") == true)
            .Where(t => !t.IsSealed || !IsRecord(t))
            .Select(t => $"{t.FullName} (sealed={t.IsSealed}, record={IsRecord(t)})")
            .ToList();

        Assert.True(violations.Count == 0,
            "FAIL: Integration event types in *.Contracts.Events.* must be declared as 'sealed record'. " +
            "See ADR-0006 (Domain vs Integration Events) and ADR-0015. " +
            $"Offending types: {string.Join(", ", violations)}");
    }

    [Fact]
    public void ModulesWithUserIdEntities_MustHavePersonalDataEraser()
    {
        // Any module assembly that contains an entity with a UserId property must also contain
        // a concrete IPersonalDataEraser, ensuring GDPR data-erasure coverage.
        // An assembly may opt out by applying [assembly: NoPersonalData] if it holds no personal data.
        // See ADR-0012 (GDPR) and ADR-0015.
        var violations = new List<string>();

        foreach (var assembly in AllModuleAssemblies)
        {
            var hasUserIdEntity = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract)
                .Any(t => t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Any(p => string.Equals(p.Name, "UserId", StringComparison.OrdinalIgnoreCase)));

            if (!hasUserIdEntity)
            {
                continue;
            }

            var hasEraser = assembly.GetTypes()
                .Any(t => !t.IsAbstract && !t.IsInterface
                       && typeof(IPersonalDataEraser).IsAssignableFrom(t));

            var hasNoPersonalDataOptOut = assembly
                .GetCustomAttributes()
                .Any(a => string.Equals(a.GetType().Name, "NoPersonalDataAttribute", StringComparison.Ordinal));

            if (!hasEraser && !hasNoPersonalDataOptOut)
            {
                violations.Add(assembly.GetName().Name!);
            }
        }

        Assert.True(violations.Count == 0,
            "FAIL: Module assemblies containing entities with a 'UserId' property must include " +
            "a concrete IPersonalDataEraser, or be marked [assembly: NoPersonalData] to opt out. " +
            "See ADR-0012 (GDPR) and ADR-0015. " +
            $"Non-compliant assemblies: {string.Join(", ", violations)}");
    }

    [Fact]
    public void SystemIOFile_MustNotBeUsedInModuleAssemblies()
    {
        // Direct System.IO.File usage is forbidden in module code.
        // All user-content file I/O must go through IBlobStore (Shared.Infrastructure).
        // The abstraction ensures the two-phase commit lifecycle is respected.
        // See ADR-0015.
        var fileType = typeof(System.IO.File);
        var violations = new List<string>();

        foreach (var assembly in AllModuleAssemblies)
        {
            var offending = assembly.GetTypes()
                .SelectMany(t => t.GetMethods(
                    BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.Static | BindingFlags.Instance |
                    BindingFlags.DeclaredOnly))
                .Where(m => m.GetMethodBody() is not null)
                .Where(m =>
                {
                    try
                    {
                        var il = m.GetMethodBody()!.GetILAsByteArray();
                        if (il is null)
                        {
                            return false;
                        }

                        var mod = m.Module;
                        for (var i = 0; i < il.Length - 4; i++)
                        {
                            if (il[i] is 0x28 or 0x6F) // call or callvirt
                            {
                                var token = BitConverter.ToInt32(il, i + 1);
                                try
                                {
                                    var resolved = mod.ResolveMethod(token);
                                    if (resolved?.DeclaringType == fileType)
                                    {
                                        return true;
                                    }
                                }
                                catch { /* unresolvable metadata token */ }
                            }
                        }

                        return false;
                    }
                    catch { return false; }
                })
                .Select(m => $"{m.DeclaringType?.FullName}.{m.Name}");

            violations.AddRange(offending);
        }

        Assert.True(violations.Count == 0,
            "FAIL: System.IO.File must not be used directly in module code. " +
            "All file I/O for user content must go through IBlobStore. " +
            "See ADR-0015 and the IBlobStore contract in Shared.Infrastructure. " +
            $"Violations: {string.Join(", ", violations)}");
    }

    private static bool IsRecord(Type t) => t.GetMethod("<Clone>$") is not null;
}
