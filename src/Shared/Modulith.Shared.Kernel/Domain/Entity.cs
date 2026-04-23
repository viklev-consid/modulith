namespace Modulith.Shared.Kernel.Domain;

// S4035: Cannot seal an intentionally abstract base class; IEqualityComparer<T> is not appropriate here.
#pragma warning disable S4035
public abstract class Entity<TId> : IEquatable<Entity<TId>>
    where TId : notnull
{
    protected Entity(TId id) => Id = id;

    public TId Id { get; private init; }

    public bool Equals(Entity<TId>? other)
    {
        if (other is null) { return false; }
        if (ReferenceEquals(this, other)) { return true; }
        if (GetType() != other.GetType()) { return false; }
        return Id.Equals(other.Id);
    }

    public override bool Equals(object? obj) => Equals(obj as Entity<TId>);

    public override int GetHashCode() => Id.GetHashCode();

    public static bool operator ==(Entity<TId>? left, Entity<TId>? right) =>
        left is not null && left.Equals(right);

    public static bool operator !=(Entity<TId>? left, Entity<TId>? right) =>
        !(left == right);
}
#pragma warning restore S4035
