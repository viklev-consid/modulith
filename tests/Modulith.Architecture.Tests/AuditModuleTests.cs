using System.Reflection;
using Modulith.Modules.Audit.Contracts.Queries;
using Modulith.Modules.Audit.Domain;
using NetArchTest.Rules;

namespace Modulith.Architecture.Tests;

[Trait("Category", "Architecture")]
public sealed class AuditModuleTests
{
    private static readonly Assembly AuditAssembly = typeof(AuditEntry).Assembly;
    private static readonly Assembly ContractsAssembly = typeof(GetAuditTrailQuery).Assembly;

    [Fact]
    public void AuditDomain_HasNoEfCoreReferences()
    {
        var result = Types.InAssembly(AuditAssembly)
            .That().ResideInNamespaceContaining(".Domain")
            .ShouldNot().HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"FAIL: Types in Audit Domain/ must not reference Microsoft.EntityFrameworkCore. " +
            $"Move EF-dependent types to Persistence/. " +
            $"Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void AuditDomain_HasNoAspNetCoreReferences()
    {
        var result = Types.InAssembly(AuditAssembly)
            .That().ResideInNamespaceContaining(".Domain")
            .ShouldNot().HaveDependencyOn("Microsoft.AspNetCore")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"FAIL: Types in Audit Domain/ must not reference Microsoft.AspNetCore. " +
            $"Move ASP.NET-dependent types out of Domain/. " +
            $"Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void AuditDomain_HasNoWolverineReferences()
    {
        var result = Types.InAssembly(AuditAssembly)
            .That().ResideInNamespaceContaining(".Domain")
            .ShouldNot().HaveDependencyOn("Wolverine")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"FAIL: Types in Audit Domain/ must not reference Wolverine. " +
            $"Messaging concerns belong in handlers or integration subscribers. " +
            $"Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void AuditEntry_HasNoPublicSetters()
    {
        var publicSetters = typeof(AuditEntry)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.SetMethod?.IsPublic == true)
            .Select(p => p.Name)
            .ToList();

        Assert.True(publicSetters.Count == 0,
            $"FAIL: AuditEntry must not have public setters. " +
            $"Found public setters on: {string.Join(", ", publicSetters)}. " +
            $"State transitions belong on aggregate methods (e.g. AnonymizeActor).");
    }

    [Fact]
    public void AuditContracts_DoesNotReferenceAuditInternal()
    {
        var referencedNames = ContractsAssembly
            .GetReferencedAssemblies()
            .Select(a => a.Name)
            .ToList();

        Assert.False(
            referencedNames.Contains(AuditAssembly.GetName().Name),
            "FAIL: Audit.Contracts must not reference the Audit internal project. " +
            "This would expose internal types to other modules. " +
            "Contracts should reference only Shared.Kernel and Shared.Contracts.");
    }

    [Fact]
    public void AuditModule_DoesNotReferenceUsersInternalProject()
    {
        var referencedNames = AuditAssembly
            .GetReferencedAssemblies()
            .Select(a => a.Name)
            .ToList();

        Assert.False(
            referencedNames.Contains("Modulith.Modules.Users"),
            "FAIL: Audit must not reference the Users internal project. " +
            "Subscribe to Users.Contracts events instead. See ADR-0005.");
    }

    [Fact]
    public void AuditModule_DoesNotReferenceCatalogInternalProject()
    {
        var referencedNames = AuditAssembly
            .GetReferencedAssemblies()
            .Select(a => a.Name)
            .ToList();

        Assert.False(
            referencedNames.Contains("Modulith.Modules.Catalog"),
            "FAIL: Audit must not reference the Catalog internal project. " +
            "Subscribe to Catalog.Contracts events instead. See ADR-0005.");
    }

    [Fact]
    public void AuditIntegrationContracts_HaveVersionSuffix()
    {
        // Audit.Contracts exposes queries and DTOs, not integration events.
        // This test guards the future: if events are added to Audit.Contracts they must be versioned.
        var eventTypes = ContractsAssembly
            .GetExportedTypes()
            .Where(t => t.Namespace?.Contains(".Events") == true)
            .Where(t => !t.Name.EndsWith("V1", StringComparison.Ordinal)
                     && !t.Name.EndsWith("V2", StringComparison.Ordinal)
                     && !t.Name.EndsWith("V3", StringComparison.Ordinal))
            .Select(t => t.Name)
            .ToList();

        Assert.True(eventTypes.Count == 0,
            $"FAIL: Integration events in Audit.Contracts/Events must have a version suffix (V1, V2, …). " +
            $"Missing suffix on: {string.Join(", ", eventTypes)}.");
    }
}
