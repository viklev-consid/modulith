using System.Reflection;
using Modulith.Modules.Audit.Contracts.Queries;
using Modulith.Modules.Catalog.Contracts.Events;
using Modulith.Modules.Users.Contracts.Events;

namespace Modulith.Architecture.Tests;

[Trait("Category", "Architecture")]
public sealed class ModuleBoundaryTests
{
    private static readonly Assembly UsersContractsAssembly = typeof(UserRegisteredV1).Assembly;
    private static readonly Assembly CatalogContractsAssembly = typeof(ProductCreatedV1).Assembly;
    private static readonly Assembly AuditContractsAssembly = typeof(GetAuditTrailQuery).Assembly;

    [Fact]
    public void ContractsAssemblies_DoNotReferenceEachOther()
    {
        // A module's .Contracts assembly must not reference another module's .Contracts assembly.
        // Each Contracts project must remain independently versionable.
        // If types need to be shared across contracts, they belong in Shared.Contracts.
        var contractsAssemblies = new[]
        {
            UsersContractsAssembly,
            CatalogContractsAssembly,
            AuditContractsAssembly,
        };

        var contractsAssemblyNames = contractsAssemblies
            .Select(a => a.GetName().Name!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var violations = new List<string>();
        foreach (var assembly in contractsAssemblies)
        {
            var crossReferences = assembly
                .GetReferencedAssemblies()
                .Where(r => contractsAssemblyNames.Contains(r.Name!)
                         && !string.Equals(r.Name, assembly.GetName().Name, StringComparison.OrdinalIgnoreCase))
                .Select(r => $"{assembly.GetName().Name} → {r.Name}");

            violations.AddRange(crossReferences);
        }

        Assert.True(violations.Count == 0,
            "FAIL: Contracts assemblies must not reference each other. " +
            "Cross-Contracts references create inter-module compile-time coupling. " +
            "Place shared types in Shared.Contracts or Shared.Kernel instead. " +
            $"Cross-references found: {string.Join(", ", violations)}");
    }

    [Fact]
    public void UsersContracts_DoesNotReferenceOtherModuleContracts()
    {
        var forbidden = new[] { "Modulith.Modules.Catalog.Contracts", "Modulith.Modules.Audit.Contracts", "Modulith.Modules.Notifications.Contracts" };
        AssertNoForbiddenReferences(UsersContractsAssembly, forbidden);
    }

    [Fact]
    public void CatalogContracts_DoesNotReferenceOtherModuleContracts()
    {
        var forbidden = new[] { "Modulith.Modules.Users.Contracts", "Modulith.Modules.Audit.Contracts", "Modulith.Modules.Notifications.Contracts" };
        AssertNoForbiddenReferences(CatalogContractsAssembly, forbidden);
    }

    [Fact]
    public void AuditContracts_DoesNotReferenceOtherModuleContracts()
    {
        var forbidden = new[] { "Modulith.Modules.Users.Contracts", "Modulith.Modules.Catalog.Contracts", "Modulith.Modules.Notifications.Contracts" };
        AssertNoForbiddenReferences(AuditContractsAssembly, forbidden);
    }

    private static void AssertNoForbiddenReferences(Assembly assembly, string[] forbiddenNames)
    {
        var referenced = assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var violations = forbiddenNames.Where(n => referenced.Contains(n)).ToList();

        Assert.True(violations.Count == 0,
            $"FAIL: {assembly.GetName().Name} must not reference other module Contracts assemblies. " +
            $"Found forbidden references: {string.Join(", ", violations)}. " +
            "See ADR-0005 and ADR-0015.");
    }
}
