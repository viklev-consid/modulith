namespace Modulith.Shared.Kernel.Interfaces;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
