namespace Modulith.Shared.Kernel.Interfaces;

public interface IRetainable
{
    TimeSpan RetentionPeriod { get; }
    DateTimeOffset RetentionStartsAt { get; }
}
