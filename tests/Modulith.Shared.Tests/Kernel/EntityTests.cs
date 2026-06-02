using Modulith.Shared.Kernel.Domain;

namespace Modulith.Shared.Tests.Kernel;

[Trait("Category", "Unit")]
public sealed class EntityTests
{
    private sealed class Widget(Guid id) : Entity<Guid>(id);

    [Fact]
    public void EqualityOperator_WithTwoNullEntities_ReturnsTrue()
    {
        Widget? left = null;
        Widget? right = null;

        Assert.True(left == right);
    }
}
