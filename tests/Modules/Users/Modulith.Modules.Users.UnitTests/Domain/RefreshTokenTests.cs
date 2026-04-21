using Modulith.Modules.Users.Domain;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Users.UnitTests.Domain;

[Trait("Category", "Unit")]
public sealed class RefreshTokenTests
{
    private static readonly UserId SomeUserId = new(Guid.NewGuid());

    [Fact]
    public void Issue_StoresHashNotRawValue()
    {
        var clock = new TokenFixedClock(DateTimeOffset.UtcNow);
        var (token, rawValue) = RefreshToken.Issue(SomeUserId, TimeSpan.FromDays(30), clock, null, null);

        var rawBytes = System.Text.Encoding.UTF8.GetBytes(rawValue);
        Assert.False(token.TokenHash.SequenceEqual(rawBytes), "TokenHash must not be the raw value.");
    }

    [Fact]
    public void Issue_HashMatchesRawValue()
    {
        var clock = new TokenFixedClock(DateTimeOffset.UtcNow);
        var (token, rawValue) = RefreshToken.Issue(SomeUserId, TimeSpan.FromDays(30), clock, null, null);

        var expectedHash = RefreshToken.HashRawValue(rawValue);
        Assert.True(token.TokenHash.SequenceEqual(expectedHash));
    }

    [Fact]
    public void IsActive_WhenNotRevokedAndNotExpired_ReturnsTrue()
    {
        var clock = new TokenFixedClock(DateTimeOffset.UtcNow);
        var (token, _) = RefreshToken.Issue(SomeUserId, TimeSpan.FromDays(30), clock, null, null);

        Assert.True(token.IsActive(clock));
    }

    [Fact]
    public void IsActive_WhenExpired_ReturnsFalse()
    {
        var clock = new TokenFixedClock(DateTimeOffset.UtcNow);
        var (token, _) = RefreshToken.Issue(SomeUserId, TimeSpan.FromDays(30), clock, null, null);

        clock.Advance(TimeSpan.FromDays(31));
        Assert.False(token.IsActive(clock));
    }

    [Fact]
    public void IsActive_AfterRevoke_ReturnsFalse()
    {
        var clock = new TokenFixedClock(DateTimeOffset.UtcNow);
        var (token, _) = RefreshToken.Issue(SomeUserId, TimeSpan.FromDays(30), clock, null, null);

        token.Revoke(clock);
        Assert.False(token.IsActive(clock));
    }

    [Fact]
    public void Revoke_IsIdempotent()
    {
        var clock = new TokenFixedClock(DateTimeOffset.UtcNow);
        var (token, _) = RefreshToken.Issue(SomeUserId, TimeSpan.FromDays(30), clock, null, null);

        var revokedAt = clock.UtcNow;
        token.Revoke(clock);
        clock.Advance(TimeSpan.FromSeconds(1));
        token.Revoke(clock); // second revoke should not change RevokedAt

        Assert.Equal(revokedAt, token.RevokedAt);
    }

    [Fact]
    public void MarkRotatedTo_SetsRevokedAtAndRotatedTo()
    {
        var clock = new TokenFixedClock(DateTimeOffset.UtcNow);
        var (token, _) = RefreshToken.Issue(SomeUserId, TimeSpan.FromDays(30), clock, null, null);
        var newTokenId = RefreshTokenId.New();

        token.MarkRotatedTo(newTokenId, clock);

        Assert.NotNull(token.RevokedAt);
        Assert.Equal(newTokenId, token.RotatedTo);
    }

    [Fact]
    public void Issue_WithUserAgentAndIp_SetsDeviceFingerprint()
    {
        var clock = new TokenFixedClock(DateTimeOffset.UtcNow);
        var (token, _) = RefreshToken.Issue(
            SomeUserId, TimeSpan.FromDays(30), clock, "Mozilla/5.0", "1.2.3.4");

        Assert.NotNull(token.DeviceFingerprint);
        Assert.NotEmpty(token.DeviceFingerprint);
    }

    [Fact]
    public void Issue_WithoutUserAgent_LeavesDeviceFingerprintNull()
    {
        var clock = new TokenFixedClock(DateTimeOffset.UtcNow);
        var (token, _) = RefreshToken.Issue(SomeUserId, TimeSpan.FromDays(30), clock, null, null);

        Assert.Null(token.DeviceFingerprint);
    }
}

file sealed class TokenFixedClock(DateTimeOffset now) : IClock
{
    private DateTimeOffset _now = now;
    public DateTimeOffset UtcNow => _now;
    public void Advance(TimeSpan duration) => _now += duration;
}
