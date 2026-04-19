using Modulith.Shared.Kernel.Domain;
using NetArchTest.Rules;

namespace Modulith.Architecture.Tests;

public sealed class SharedKernelTests
{
    private static readonly Types KernelTypes = Types.InAssembly(typeof(DomainEvent).Assembly);

    [Fact]
    [Trait("Category", "Architecture")]
    public void SharedKernel_DoesNotDependOn_EntityFrameworkCore()
    {
        var result = KernelTypes
            .Should()
            .NotHaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        Assert.True(result.IsSuccessful,
            "FAIL: Modulith.Shared.Kernel depends on Microsoft.EntityFrameworkCore.\n" +
            "Shared.Kernel must remain free of persistence infrastructure.\n" +
            "Move EF Core-dependent code to Shared.Infrastructure or a module's Persistence layer.\n" +
            $"Offending types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    [Trait("Category", "Architecture")]
    public void SharedKernel_DoesNotDependOn_AspNetCore()
    {
        var result = KernelTypes
            .Should()
            .NotHaveDependencyOn("Microsoft.AspNetCore")
            .GetResult();

        Assert.True(result.IsSuccessful,
            "FAIL: Modulith.Shared.Kernel depends on Microsoft.AspNetCore.\n" +
            "Shared.Kernel must remain free of web framework dependencies.\n" +
            "Move ASP.NET Core-dependent code to Shared.Infrastructure or the API project.\n" +
            $"Offending types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    [Trait("Category", "Architecture")]
    public void SharedKernel_DoesNotDependOn_Wolverine()
    {
        var result = KernelTypes
            .Should()
            .NotHaveDependencyOn("Wolverine")
            .GetResult();

        Assert.True(result.IsSuccessful,
            "FAIL: Modulith.Shared.Kernel depends on Wolverine.\n" +
            "Shared.Kernel must remain free of messaging infrastructure.\n" +
            "Move Wolverine-dependent code to Shared.Infrastructure or module integration layers.\n" +
            $"Offending types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    [Trait("Category", "Architecture")]
    public void SharedKernel_DoesNotDependOn_FluentValidation()
    {
        var result = KernelTypes
            .Should()
            .NotHaveDependencyOn("FluentValidation")
            .GetResult();

        Assert.True(result.IsSuccessful,
            "FAIL: Modulith.Shared.Kernel depends on FluentValidation.\n" +
            "Shared.Kernel must remain free of validation framework dependencies.\n" +
            "Move FluentValidation-dependent code to module feature slices.\n" +
            $"Offending types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }
}
