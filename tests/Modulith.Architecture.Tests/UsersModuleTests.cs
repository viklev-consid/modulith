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
    public void UsersDomain_HasNoFluentValidationReferences()
    {
        var result = Types.InAssembly(UsersAssembly)
            .That().ResideInNamespaceContaining(".Domain")
            .ShouldNot().HaveDependencyOn("FluentValidation")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"FAIL: Types in Domain/ must not reference FluentValidation. " +
            $"Domain validation uses the Result pattern; FluentValidation belongs in feature slice Validators. " +
            $"Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void UsersDomain_HasNoFeatureManagementReferences()
    {
        var result = Types.InAssembly(UsersAssembly)
            .That().ResideInNamespaceContaining(".Domain")
            .ShouldNot().HaveDependencyOn("Microsoft.FeatureManagement")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"FAIL: Types in Domain/ must not reference Microsoft.FeatureManagement. " +
            $"Feature flags belong at the edges (endpoint routing, handler selection). " +
            $"Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void UsersDomain_HasNoCachingReferences()
    {
        var result = Types.InAssembly(UsersAssembly)
            .That().ResideInNamespaceContaining(".Domain")
            .ShouldNot().HaveDependencyOn("Microsoft.Extensions.Caching")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"FAIL: Types in Domain/ must not reference Microsoft.Extensions.Caching. " +
            $"Cache interaction belongs in handlers and infrastructure, not domain types. " +
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
    public void RefreshToken_HasNoPublicSetters()
    {
        var publicSetters = typeof(RefreshToken)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.SetMethod?.IsPublic == true)
            .Select(p => p.Name)
            .ToList();

        Assert.True(publicSetters.Count == 0,
            $"FAIL: RefreshToken entity must not have public setters. " +
            $"Found public setters on: {string.Join(", ", publicSetters)}.");
    }

    [Fact]
    public void SingleUseToken_HasNoPublicSetters()
    {
        var publicSetters = typeof(SingleUseToken)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.SetMethod?.IsPublic == true)
            .Select(p => p.Name)
            .ToList();

        Assert.True(publicSetters.Count == 0,
            $"FAIL: SingleUseToken entity must not have public setters. " +
            $"Found public setters on: {string.Join(", ", publicSetters)}.");
    }

    [Fact]
    public void PendingEmailChange_HasNoPublicSetters()
    {
        var publicSetters = typeof(PendingEmailChange)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.SetMethod?.IsPublic == true)
            .Select(p => p.Name)
            .ToList();

        Assert.True(publicSetters.Count == 0,
            $"FAIL: PendingEmailChange entity must not have public setters. " +
            $"Found public setters on: {string.Join(", ", publicSetters)}.");
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

    [Fact]
    public void UsersModule_DoesNotUseRawDateTimeUtcNow()
    {
        // Verify no production source uses DateTime.UtcNow directly in the Users module.
        // All code must use IClock to allow testability and deterministic security invariants.
        //
        // This test checks for the method reference via IL inspection — any direct call to
        // DateTime.get_UtcNow in non-test code in the Users assembly is a violation.
        var violations = UsersAssembly.GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
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

                    var module = m.Module;
                    // Check if DateTime.UtcNow is referenced in this method's metadata tokens
                    for (int i = 0; i < il.Length - 4; i++)
                    {
                        if (il[i] is 0x28 or 0x6F) // call or callvirt opcode
                        {
                            var token = System.BitConverter.ToInt32(il, i + 1);
                            try
                            {
                                var resolved = module.ResolveMethod(token);
                                if (resolved?.DeclaringType == typeof(DateTime) &&
                                    resolved.Name == "get_UtcNow")
                                {
                                    return true;
                                }
                            }
                            catch { /* token may not resolve */ }
                        }
                    }
                    return false;
                }
                catch { return false; }
            })
            .Select(m => $"{m.DeclaringType?.Name}.{m.Name}")
            .ToList();

        Assert.True(violations.Count == 0,
            $"FAIL: DateTime.UtcNow must not be used directly in the Users module. " +
            $"Inject and use IClock instead (required for testability and token expiry correctness). " +
            $"Violations: {string.Join(", ", violations)}");
    }
}
