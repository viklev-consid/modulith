namespace Modulith.Shared.Kernel.Identifiers;

public abstract record TypedId<T>(T Value)
    where T : notnull
{
    public sealed override string ToString() => Value.ToString() ?? string.Empty;
}
