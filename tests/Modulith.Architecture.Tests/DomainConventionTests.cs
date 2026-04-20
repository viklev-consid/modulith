using Modulith.Shared.Kernel.Domain;
using NetArchTest.Rules;

namespace Modulith.Architecture.Tests;

public sealed class DomainConventionTests
{
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
}
