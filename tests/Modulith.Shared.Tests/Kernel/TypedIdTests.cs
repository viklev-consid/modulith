using Modulith.Shared.Kernel.Identifiers;

namespace Modulith.Shared.Tests.Kernel;

[Trait("Category", "Unit")]
public sealed class TypedIdTests
{
    private sealed record TestId(Guid Value) : TypedId<Guid>(Value)
    {
        public static TestId New() => new(Guid.NewGuid());
        public static TestId From(Guid value) => new(value);
    }

    [Fact]
    public void TwoIds_WithSameValue_AreEqual()
    {
        var guid = Guid.NewGuid();
        var id1 = TestId.From(guid);
        var id2 = TestId.From(guid);

        Assert.Equal(id1, id2);
    }

    [Fact]
    public void TwoIds_WithDifferentValues_AreNotEqual()
    {
        var id1 = TestId.New();
        var id2 = TestId.New();

        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void ToString_ReturnsGuidString()
    {
        var guid = Guid.NewGuid();
        var id = TestId.From(guid);

        Assert.Equal(guid.ToString(), id.ToString());
    }

    [Fact]
    public void New_GeneratesUniqueIds()
    {
        var id1 = TestId.New();
        var id2 = TestId.New();

        Assert.NotEqual(id1.Value, id2.Value);
    }
}
