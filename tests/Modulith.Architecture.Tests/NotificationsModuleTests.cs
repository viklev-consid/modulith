using System.Reflection;
using Modulith.Modules.Audit.Domain;
using Modulith.Modules.Catalog.Domain;
using Modulith.Modules.Notifications.Domain;
using Modulith.Modules.Users.Domain;
using NetArchTest.Rules;

namespace Modulith.Architecture.Tests;

[Trait("Category", "Architecture")]
public sealed class NotificationsModuleTests
{
    private static readonly Assembly NotificationsAssembly = typeof(NotificationLog).Assembly;

    [Fact]
    public void NotificationsDomain_HasNoEfCoreReferences()
    {
        var result = Types.InAssembly(NotificationsAssembly)
            .That().ResideInNamespaceContaining(".Domain")
            .ShouldNot().HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"FAIL: Types in Notifications Domain/ must not reference Microsoft.EntityFrameworkCore. " +
            $"Move EF-dependent types to Persistence/. " +
            $"Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void NotificationsDomain_HasNoAspNetCoreReferences()
    {
        var result = Types.InAssembly(NotificationsAssembly)
            .That().ResideInNamespaceContaining(".Domain")
            .ShouldNot().HaveDependencyOn("Microsoft.AspNetCore")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"FAIL: Types in Notifications Domain/ must not reference Microsoft.AspNetCore. " +
            $"Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void NotificationsDomain_HasNoWolverineReferences()
    {
        var result = Types.InAssembly(NotificationsAssembly)
            .That().ResideInNamespaceContaining(".Domain")
            .ShouldNot().HaveDependencyOn("Wolverine")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"FAIL: Types in Notifications Domain/ must not reference Wolverine. " +
            $"Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void NotificationsDomain_HasNoFluentValidationReferences()
    {
        var result = Types.InAssembly(NotificationsAssembly)
            .That().ResideInNamespaceContaining(".Domain")
            .ShouldNot().HaveDependencyOn("FluentValidation")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"FAIL: Types in Notifications Domain/ must not reference FluentValidation. " +
            $"Domain validation uses the Result pattern; FluentValidation belongs in feature slice Validators. " +
            $"Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void NotificationsDomain_HasNoFeatureManagementReferences()
    {
        var result = Types.InAssembly(NotificationsAssembly)
            .That().ResideInNamespaceContaining(".Domain")
            .ShouldNot().HaveDependencyOn("Microsoft.FeatureManagement")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"FAIL: Types in Notifications Domain/ must not reference Microsoft.FeatureManagement. " +
            $"Feature flags belong at the edges (endpoint routing, handler selection). " +
            $"Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void NotificationsDomain_HasNoCachingReferences()
    {
        var result = Types.InAssembly(NotificationsAssembly)
            .That().ResideInNamespaceContaining(".Domain")
            .ShouldNot().HaveDependencyOn("Microsoft.Extensions.Caching")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"FAIL: Types in Notifications Domain/ must not reference Microsoft.Extensions.Caching. " +
            $"Cache interaction belongs in handlers and infrastructure, not domain types. " +
            $"Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void NotificationLog_HasNoPublicSetters()
    {
        var publicSetters = typeof(NotificationLog)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.SetMethod?.IsPublic == true)
            .Select(p => p.Name)
            .ToList();

        Assert.True(publicSetters.Count == 0,
            $"FAIL: NotificationLog must not have public setters. " +
            $"Found public setters on: {string.Join(", ", publicSetters)}.");
    }

    [Fact]
    public void NotificationsModule_DoesNotReferenceUsersInternalProject()
    {
        var referencedNames = NotificationsAssembly
            .GetReferencedAssemblies()
            .Select(a => a.Name)
            .ToList();

        Assert.False(
            referencedNames.Contains("Modulith.Modules.Users"),
            "FAIL: Notifications must not reference the Users internal project. " +
            "Subscribe to Users.Contracts events instead. See ADR-0005.");
    }

    [Fact]
    public void NotificationsModule_DoesNotReferenceCatalogInternalProject()
    {
        var referencedNames = NotificationsAssembly
            .GetReferencedAssemblies()
            .Select(a => a.Name)
            .ToList();

        Assert.False(
            referencedNames.Contains("Modulith.Modules.Catalog"),
            "FAIL: Notifications must not reference the Catalog internal project. " +
            "Subscribe to Catalog.Contracts events instead. See ADR-0005.");
    }

    [Fact]
    public void IEmailSender_IsNotUsedOutsideNotificationsAndSharedInfrastructure()
    {
        // IEmailSender lives in Modulith.Shared.Infrastructure.Notifications.
        // Only the Notifications module (and Shared.Infrastructure itself) should use it.
        // Other modules communicate notification needs by publishing events that Notifications subscribes to.
        var otherModuleAssemblies = new[]
        {
            typeof(User).Assembly,
            typeof(Product).Assembly,
            typeof(AuditEntry).Assembly,
        };

        var violations = new List<string>();
        foreach (var assembly in otherModuleAssemblies)
        {
            var result = Types.InAssembly(assembly)
                .ShouldNot()
                .HaveDependencyOn("Modulith.Shared.Infrastructure.Notifications")
                .GetResult();

            if (!result.IsSuccessful)
            {
                violations.AddRange(result.FailingTypeNames ?? []);
            }
        }

        Assert.True(violations.Count == 0,
            "FAIL: IEmailSender / ISmsSender must only be used inside Modulith.Modules.Notifications " +
            "and Modulith.Shared.Infrastructure. " +
            "Other modules must publish domain events and let Notifications handle delivery. " +
            $"Offending types: {string.Join(", ", violations)}");
    }
}
