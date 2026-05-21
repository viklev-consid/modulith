using Modulith.Modules.Organizations.Domain;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Organizations.UnitTests.Domain;

[Trait("Category", "Unit")]
public sealed class OrganizationInvitationTests
{
    private static readonly OrganizationId organizationId = new(Guid.NewGuid());
    private static readonly Guid invitedByUserId = Guid.NewGuid();
    private static readonly Guid acceptedUserId = Guid.NewGuid();
    private readonly InvitationClock clock = new(DateTimeOffset.UtcNow);

    [Fact]
    public void Create_StoresHashNotRawValue()
    {
        var result = OrganizationInvitation.Create(
            organizationId,
            "alice@example.com",
            OrganizationRole.Member,
            TimeSpan.FromDays(7),
            invitedByUserId,
            clock);

        Assert.False(result.IsError);
        var (invitation, rawToken) = result.Value;
        var rawBytes = System.Text.Encoding.UTF8.GetBytes(rawToken);
        Assert.False(invitation.TokenHash.SequenceEqual(rawBytes));
        Assert.True(invitation.TokenHash.SequenceEqual(OrganizationInvitation.HashRawValue(rawToken)));
    }

    [Fact]
    public void Accept_WhenEmailMatches_Succeeds()
    {
        var invitation = CreateInvitation();

        var result = invitation.Accept(acceptedUserId, "alice@example.com", clock);

        Assert.False(result.IsError);
        Assert.False(invitation.IsPending);
        Assert.Equal(acceptedUserId, invitation.AcceptedUserId);
    }

    [Fact]
    public void Accept_WhenEmailDoesNotMatch_ReturnsError()
    {
        var invitation = CreateInvitation();

        var result = invitation.Accept(acceptedUserId, "bob@example.com", clock);

        Assert.True(result.IsError);
    }

    [Fact]
    public void Accept_WhenExpired_ReturnsError()
    {
        var invitation = CreateInvitation();
        clock.Advance(TimeSpan.FromDays(8));

        var result = invitation.Accept(acceptedUserId, "alice@example.com", clock);

        Assert.True(result.IsError);
    }

    private OrganizationInvitation CreateInvitation() =>
        OrganizationInvitation.Create(
            organizationId,
            "alice@example.com",
            OrganizationRole.Member,
            TimeSpan.FromDays(7),
            invitedByUserId,
            clock).Value.Invitation;
}

internal sealed class InvitationClock(DateTimeOffset now) : IClock
{
    private DateTimeOffset now = now;
    public DateTimeOffset UtcNow => now;
    public void Advance(TimeSpan duration) => now += duration;
}
