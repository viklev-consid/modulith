using System.Security.Cryptography;
using Modulith.Modules.Users.Security;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Users.UnitTests.Security;

[Trait("Category", "Unit")]
public sealed class TotpServiceTests
{
    [Fact]
    public void Verify_WithOneStepDrift_AcceptsPreviousCurrentAndNextSteps()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);
        var clock = new FixedClock(now);
        var service = new TotpService(clock);
        const string secret = "JBSWY3DPEHPK3PXP";

        Assert.True(service.Verify(secret, ComputeCode(secret, now.AddSeconds(-30)), allowedTimeStepDrift: 1).IsValid);
        Assert.True(service.Verify(secret, ComputeCode(secret, now), allowedTimeStepDrift: 1).IsValid);
        Assert.True(service.Verify(secret, ComputeCode(secret, now.AddSeconds(30)), allowedTimeStepDrift: 1).IsValid);
    }

    [Fact]
    public void Verify_WithOneStepDrift_RejectsCodeTwoStepsAway()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);
        var clock = new FixedClock(now);
        var service = new TotpService(clock);
        const string secret = "JBSWY3DPEHPK3PXP";

        Assert.False(service.Verify(secret, ComputeCode(secret, now.AddSeconds(-60)), allowedTimeStepDrift: 1).IsValid);
        Assert.False(service.Verify(secret, ComputeCode(secret, now.AddSeconds(60)), allowedTimeStepDrift: 1).IsValid);
    }

    private static string ComputeCode(string secret, DateTimeOffset timestamp)
    {
        var secretBytes = Base32Decode(secret);
        var timeStep = timestamp.ToUnixTimeSeconds() / 30;
        Span<byte> counter = stackalloc byte[8];
        BitConverter.TryWriteBytes(counter, timeStep);
        if (BitConverter.IsLittleEndian)
        {
            counter.Reverse();
        }

#pragma warning disable CA5350
        using var hmac = new HMACSHA1(secretBytes);
#pragma warning restore CA5350
        var hash = hmac.ComputeHash(counter.ToArray());
        var offset = hash[^1] & 0x0f;
        var binary =
            ((hash[offset] & 0x7f) << 24)
            | ((hash[offset + 1] & 0xff) << 16)
            | ((hash[offset + 2] & 0xff) << 8)
            | (hash[offset + 3] & 0xff);

        return (binary % 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static byte[] Base32Decode(string value)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bytes = new List<byte>();
        var bitBuffer = 0;
        var bitCount = 0;

        foreach (var c in value.Trim().ToUpperInvariant())
        {
            var index = alphabet.IndexOf(c, StringComparison.Ordinal);
            bitBuffer = (bitBuffer << 5) | index;
            bitCount += 5;

            if (bitCount >= 8)
            {
                bytes.Add((byte)((bitBuffer >> (bitCount - 8)) & 0xff));
                bitCount -= 8;
            }
        }

        return bytes.ToArray();
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
