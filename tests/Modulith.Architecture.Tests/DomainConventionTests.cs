using System.Reflection;
using Modulith.Shared.Kernel.Domain;
using NetArchTest.Rules;

namespace Modulith.Architecture.Tests;

public sealed class DomainConventionTests
{
    private static readonly IReadOnlyList<Assembly> allModuleAssemblies =
        ModuleAssemblyCatalog.ModuleAssemblies;

    [Fact]
    [Trait("Category", "Architecture")]
    public void ConcreteDomainEvents_MustResideInDomainEventsNamespace()
    {
        var result = Types.InAssembly(typeof(DomainEvent).Assembly)
            .That()
            .Inherit(typeof(DomainEvent))
            .And()
            .AreNotAbstract()
            .Should()
            .ResideInNamespaceContaining(".Domain.Events")
            .GetResult();

        Assert.True(result.IsSuccessful,
            "FAIL: Concrete DomainEvent types must reside in a '*.Domain.Events.*' namespace.\n" +
            "Place domain event types under a 'Domain/Events/' folder.\n" +
            "Namespace pattern: '<Module>.Domain.Events'\n\n" +
            $"Offending types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    [Trait("Category", "Architecture")]
    public void AggregatesAndEntities_MustHaveNoPublicSetters()
    {
        var aggregateRootBase = typeof(AggregateRoot<>);
        var entityBase = typeof(Entity<>);

        var violations = allModuleAssemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => InheritsFromGenericBase(t, aggregateRootBase)
                     || InheritsFromGenericBase(t, entityBase))
            .SelectMany(t => t
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.SetMethod?.IsPublic == true)
                .Select(p => $"{t.FullName}.{p.Name}"))
            .ToList();

        Assert.True(violations.Count == 0,
            "FAIL: Aggregate and Entity types must not have public setters. " +
            "State transitions belong on aggregate/entity methods so invariants cannot be bypassed. " +
            "Use private setters/init-only members for EF Core hydration where needed. " +
            "See ADR-0009 (Rich Domain Model). " +
            $"Offending properties: {string.Join(", ", violations)}");
    }

    private static bool InheritsFromGenericBase(Type type, Type genericBase)
    {
        var current = type.BaseType;
        while (current is not null)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == genericBase)
            {
                return true;
            }

            current = current.BaseType;
        }

        return false;
    }
}
