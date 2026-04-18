using Modulith.Shared.Kernel.Domain;
using Modulith.Shared.Kernel.Identifiers;

namespace Modulith.Shared.Tests.Kernel;

[Trait("Category", "Unit")]
public sealed class AggregateRootTests
{
    private sealed record WidgetId(Guid Value) : TypedId<Guid>(Value)
    {
        public static WidgetId New() => new(Guid.NewGuid());
    }

    private sealed record WidgetCreated(WidgetId Id, string Name) : DomainEvent;

    private sealed record WidgetRenamed(WidgetId Id, string OldName, string NewName) : DomainEvent;

    private sealed class Widget : AggregateRoot<WidgetId>
    {
        private Widget(WidgetId id, string name) : base(id)
        {
            Name = name;
            RaiseEvent(new WidgetCreated(id, name));
        }

        public string Name { get; private set; }

        public static Widget Create(string name) => new(WidgetId.New(), name);

        public void Rename(string newName)
        {
            var oldName = Name;
            Name = newName;
            RaiseEvent(new WidgetRenamed(Id, oldName, newName));
        }
    }

    [Fact]
    public void Create_RaisesCreatedEvent()
    {
        var widget = Widget.Create("Sprocket");

        Assert.Single(widget.DomainEvents);
        Assert.IsType<WidgetCreated>(widget.DomainEvents.First());
    }

    [Fact]
    public void Rename_RaisesRenamedEvent()
    {
        var widget = Widget.Create("Sprocket");
        widget.ClearDomainEvents();

        widget.Rename("Cog");

        Assert.Single(widget.DomainEvents);
        var evt = Assert.IsType<WidgetRenamed>(widget.DomainEvents.First());
        Assert.Equal("Sprocket", evt.OldName);
        Assert.Equal("Cog", evt.NewName);
    }

    [Fact]
    public void ClearDomainEvents_RemovesAllEvents()
    {
        var widget = Widget.Create("Sprocket");
        Assert.NotEmpty(widget.DomainEvents);

        widget.ClearDomainEvents();

        Assert.Empty(widget.DomainEvents);
    }

    [Fact]
    public void MultipleOperations_AccumulateEvents()
    {
        var widget = Widget.Create("A");
        widget.Rename("B");
        widget.Rename("C");

        Assert.Equal(3, widget.DomainEvents.Count);
    }

    [Fact]
    public void DomainEvent_HasUniqueEventId()
    {
        var widget1 = Widget.Create("X");
        var widget2 = Widget.Create("Y");

        var id1 = widget1.DomainEvents.First().EventId;
        var id2 = widget2.DomainEvents.First().EventId;

        Assert.NotEqual(id1, id2);
    }
}
