using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.TestSupport;

public sealed class TestClock : IClock
{
    private DateTimeOffset utcNow = DateTimeOffset.UtcNow;

    public DateTimeOffset UtcNow => utcNow;

    public void Set(DateTimeOffset value) => utcNow = value;

    public void Advance(TimeSpan duration) => utcNow += duration;
}
