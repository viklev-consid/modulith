using System.Reflection;
using Modulith.Modules.Users.Contracts.Events;
using Modulith.Modules.Users.Domain;
using NetArchTest.Rules;

namespace Modulith.Architecture.Tests;

[Trait("Category", "Architecture")]
public sealed class UsersModuleTests
{
    private static readonly Assembly UsersAssembly = typeof(User).Assembly;
    private static readonly Assembly ContractsAssembly = typeof(UserRegisteredV1).Assembly;

    [Fact]
    public void UsersDomain_HasNoEfCoreReferences()
    {
        var result = Types.InAssembly(UsersAssembly)
            .That().ResideInNamespaceContaining(".Domain")
            .ShouldNot().HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"FAIL: Types in Domain/ must not reference Microsoft.EntityFrameworkCore. " +
            $"Move EF-dependent types to Persistence/. " +
            $"Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void UsersDomain_HasNoAspNetCoreReferences()
    {
        var result = Types.InAssembly(UsersAssembly)
            .That().ResideInNamespaceContaining(".Domain")
            .ShouldNot().HaveDependencyOn("Microsoft.AspNetCore")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"FAIL: Types in Domain/ must not reference Microsoft.AspNetCore. " +
            $"Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void UsersDomain_HasNoWolverineReferences()
    {
        var result = Types.InAssembly(UsersAssembly)
            .That().ResideInNamespaceContaining(".Domain")
            .ShouldNot().HaveDependencyOn("Wolverine")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"FAIL: Types in Domain/ must not reference Wolverine. " +
            $"Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void User_HasNoPublicSetters()
    {
        var publicSetters = typeof(User)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.SetMethod?.IsPublic == true)
            .Select(p => p.Name)
            .ToList();

        Assert.True(publicSetters.Count == 0,
            $"FAIL: User aggregate must not have public setters. " +
            $"Found public setters on: {string.Join(", ", publicSetters)}. " +
            $"State transitions belong on aggregate methods.");
    }

    [Fact]
    public void UsersContracts_DoesNotReferenceUsersInternal()
    {
        var referencedNames = ContractsAssembly
            .GetReferencedAssemblies()
            .Select(a => a.Name)
            .ToList();

        Assert.False(
            referencedNames.Contains(UsersAssembly.GetName().Name),
            "FAIL: Users.Contracts must not reference Users internal project. " +
            "This would expose internal types to other modules. " +
            "Contracts should reference only Shared.Kernel and Shared.Contracts.");
    }

    [Fact]
    public void IntegrationEvents_HaveVersionSuffix()
    {
        var eventTypes = ContractsAssembly
            .GetExportedTypes()
            .Where(t => t.Namespace?.Contains(".Events") == true)
            .Where(t => !t.Name.EndsWith("V1", StringComparison.Ordinal)
                     && !t.Name.EndsWith("V2", StringComparison.Ordinal)
                     && !t.Name.EndsWith("V3", StringComparison.Ordinal))
            .Select(t => t.Name)
            .ToList();

        Assert.True(eventTypes.Count == 0,
            $"FAIL: Integration events in Contracts/Events must have a version suffix (V1, V2, …). " +
            $"Missing suffix on: {string.Join(", ", eventTypes)}.");
    }
}
