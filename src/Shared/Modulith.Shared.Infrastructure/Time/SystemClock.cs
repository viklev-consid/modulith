using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Shared.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
