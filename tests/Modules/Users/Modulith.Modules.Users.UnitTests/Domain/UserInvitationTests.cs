using Modulith.Modules.Users.Domain;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Users.UnitTests.Domain;

[Trait("Category", "Unit")]
public sealed class UserInvitationTests
{
    private static Email ValidEmail => Email.Create("alice@example.com").Value;
    private static readonly UserId someUserId = new(Guid.NewGuid());

    [Fact]
    public void Create_StoresHashNotRawValue()
    {
        var clock = new InvitationClock(DateTimeOffset.UtcNow);

        var result = UserInvitation.Create(ValidEmail, TimeSpan.FromDays(7), clock);

        Assert.False(result.IsError);
        var (invitation, rawToken) = result.Value;
        var rawBytes = System.Text.Encoding.UTF8.GetBytes(rawToken);
        Assert.False(invitation.TokenHash.SequenceEqual(rawBytes));
    }

    [Fact]
    public void Create_HashMatchesRawValue()
    {
        var clock = new InvitationClock(DateTimeOffset.UtcNow);

        var result = UserInvitation.Create(ValidEmail, TimeSpan.FromDays(7), clock);

        var (invitation, rawToken) = result.Value;
        Assert.True(invitation.TokenHash.SequenceEqual(UserInvitation.HashRawValue(rawToken)));
    }

    [Fact]
    public void Accept_WhenPendingAndEmailMatches_Succeeds()
    {
        var clock = new InvitationClock(DateTimeOffset.UtcNow);
        var invitation = UserInvitation.Create(ValidEmail, TimeSpan.FromDays(7), clock).Value.invitation;

        var result = invitation.Accept(someUserId, ValidEmail, clock);

        Assert.False(result.IsError);
        Assert.NotNull(invitation.AcceptedAt);
        Assert.Equal(someUserId, invitation.AcceptedUserId);
    }

    [Fact]
    public void Accept_WhenEmailDoesNotMatch_ReturnsError()
    {
        var clock = new InvitationClock(DateTimeOffset.UtcNow);
        var invitation = UserInvitation.Create(ValidEmail, TimeSpan.FromDays(7), clock).Value.invitation;
        var otherEmail = Email.Create("bob@example.com").Value;

        var result = invitation.Accept(someUserId, otherEmail, clock);

        Assert.True(result.IsError);
    }

    [Fact]
    public void Accept_WhenExpired_ReturnsError()
    {
        var clock = new InvitationClock(DateTimeOffset.UtcNow);
        var invitation = UserInvitation.Create(ValidEmail, TimeSpan.FromDays(7), clock).Value.invitation;

        clock.Advance(TimeSpan.FromDays(8));
        var result = invitation.Accept(someUserId, ValidEmail, clock);

        Assert.True(result.IsError);
    }

    [Fact]
    public void Accept_WhenAlreadyAccepted_ReturnsError()
    {
        var clock = new InvitationClock(DateTimeOffset.UtcNow);
        var invitation = UserInvitation.Create(ValidEmail, TimeSpan.FromDays(7), clock).Value.invitation;

        invitation.Accept(someUserId, ValidEmail, clock);
        var result = invitation.Accept(someUserId, ValidEmail, clock);

        Assert.True(result.IsError);
    }

    [Fact]
    public void Accept_WhenRevoked_ReturnsError()
    {
        var clock = new InvitationClock(DateTimeOffset.UtcNow);
        var invitation = UserInvitation.Create(ValidEmail, TimeSpan.FromDays(7), clock).Value.invitation;

        invitation.Revoke(clock);
        var result = invitation.Accept(someUserId, ValidEmail, clock);

        Assert.True(result.IsError);
    }

    [Fact]
    public void Revoke_WhenPending_Succeeds()
    {
        var clock = new InvitationClock(DateTimeOffset.UtcNow);
        var invitation = UserInvitation.Create(ValidEmail, TimeSpan.FromDays(7), clock).Value.invitation;

        var result = invitation.Revoke(clock);

        Assert.False(result.IsError);
        Assert.NotNull(invitation.RevokedAt);
    }
}

file sealed class InvitationClock(DateTimeOffset now) : IClock
{
    private DateTimeOffset now = now;
    public DateTimeOffset UtcNow => now;
    public void Advance(TimeSpan duration) => now += duration;
}
