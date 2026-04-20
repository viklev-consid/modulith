using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.TestSupport;

public sealed class TestClock : IClock
{
    private DateTimeOffset _utcNow = DateTimeOffset.UtcNow;

    public DateTimeOffset UtcNow => _utcNow;

    public void Set(DateTimeOffset value) => _utcNow = value;

    public void Advance(TimeSpan duration) => _utcNow += duration;
}
