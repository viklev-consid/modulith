using Modulith.Modules.Users.Domain;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Users.UnitTests.Domain;

[Trait("Category", "Unit")]
public sealed class SingleUseTokenTests
{
    private static readonly UserId SomeUserId = new(Guid.NewGuid());

    [Fact]
    public void Create_StoresHashNotRawValue()
    {
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var (token, rawValue) = SingleUseToken.Create(SomeUserId, TokenPurpose.PasswordReset, TimeSpan.FromMinutes(30), clock);

        var rawBytes = System.Text.Encoding.UTF8.GetBytes(rawValue);
        Assert.False(token.TokenHash.SequenceEqual(rawBytes), "TokenHash must not be the raw value.");
    }

    [Fact]
    public void Create_HashMatchesRawValue()
    {
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var (token, rawValue) = SingleUseToken.Create(SomeUserId, TokenPurpose.PasswordReset, TimeSpan.FromMinutes(30), clock);

        var expectedHash = SingleUseToken.HashRawValue(rawValue);
        Assert.True(token.TokenHash.SequenceEqual(expectedHash));
    }

    [Fact]
    public void IsValid_WhenNotConsumedAndNotExpired_ReturnsTrue()
    {
        var now = DateTimeOffset.UtcNow;
        var clock = new FixedClock(now);
        var (token, _) = SingleUseToken.Create(SomeUserId, TokenPurpose.PasswordReset, TimeSpan.FromMinutes(30), clock);

        Assert.True(token.IsValid(clock));
    }

    [Fact]
    public void IsValid_WhenExpired_ReturnsFalse()
    {
        var now = DateTimeOffset.UtcNow;
        var clock = new FixedClock(now);
        var (token, _) = SingleUseToken.Create(SomeUserId, TokenPurpose.PasswordReset, TimeSpan.FromMinutes(30), clock);

        clock.Advance(TimeSpan.FromMinutes(31));
        Assert.False(token.IsValid(clock));
    }

    [Fact]
    public void IsValid_AfterConsume_ReturnsFalse()
    {
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var (token, _) = SingleUseToken.Create(SomeUserId, TokenPurpose.PasswordReset, TimeSpan.FromMinutes(30), clock);

        token.Consume(clock);
        Assert.False(token.IsValid(clock));
    }

    [Fact]
    public void Consume_WhenValid_Succeeds()
    {
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var (token, _) = SingleUseToken.Create(SomeUserId, TokenPurpose.PasswordReset, TimeSpan.FromMinutes(30), clock);

        var result = token.Consume(clock);
        Assert.False(result.IsError);
        Assert.NotNull(token.ConsumedAt);
    }

    [Fact]
    public void Consume_WhenExpired_ReturnsError()
    {
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var (token, _) = SingleUseToken.Create(SomeUserId, TokenPurpose.PasswordReset, TimeSpan.FromMinutes(30), clock);

        clock.Advance(TimeSpan.FromMinutes(31));
        var result = token.Consume(clock);
        Assert.True(result.IsError);
    }

    [Fact]
    public void Consume_WhenAlreadyConsumed_ReturnsError()
    {
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var (token, _) = SingleUseToken.Create(SomeUserId, TokenPurpose.PasswordReset, TimeSpan.FromMinutes(30), clock);

        token.Consume(clock);
        var result = token.Consume(clock);
        Assert.True(result.IsError);
    }
}

file sealed class FixedClock(DateTimeOffset now) : IClock
{
    private DateTimeOffset _now = now;
    public DateTimeOffset UtcNow => _now;
    public void Advance(TimeSpan duration) => _now += duration;
}
