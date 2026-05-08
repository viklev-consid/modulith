using System.Reflection;

namespace Modulith.Architecture.Tests;

[Trait("Category", "Architecture")]
public sealed class ModuleBoundaryTests
{
    private static readonly IReadOnlyList<Assembly> contractsAssemblies =
        ModuleAssemblyCatalog.ContractsAssemblies;

    [Fact]
    public void ContractsAssemblies_DoNotReferenceEachOther()
    {
        // A module's .Contracts assembly must not reference another module's .Contracts assembly.
        // Each Contracts project must remain independently versionable.
        // If types need to be shared across contracts, they belong in Shared.Contracts.
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
    public void ContractsAssemblies_DoNotReferenceOtherModuleContracts()
    {
        foreach (var assembly in contractsAssemblies)
        {
            var forbidden = contractsAssemblies
                .Where(a => !string.Equals(
                    a.GetName().Name,
                    assembly.GetName().Name,
                    StringComparison.OrdinalIgnoreCase))
                .Select(a => a.GetName().Name!)
                .ToArray();

            AssertNoForbiddenReferences(assembly, forbidden);
        }
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
