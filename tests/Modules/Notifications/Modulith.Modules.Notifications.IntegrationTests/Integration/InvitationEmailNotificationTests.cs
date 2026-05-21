using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Modules.Notifications.Domain;
using Modulith.Modules.Notifications.Persistence;
using Modulith.Modules.Organizations.Contracts.Events;
using Modulith.Modules.Users.Contracts.Events;
using Wolverine.Tracking;

namespace Modulith.Modules.Notifications.IntegrationTests.Integration;

[Collection("NotificationsCrossModule")]
[Trait("Category", "Integration")]
public sealed class InvitationEmailNotificationTests(NotificationsCrossModuleFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task UserInvitationCreated_SendsInvitationEmail()
    {
        var eventId = Guid.NewGuid();
        var invitedByUserId = Guid.NewGuid();
        var invitation = new UserInvitationCreatedV1(
            Guid.NewGuid(),
            "invitee@example.com",
            "user-token",
            DateTimeOffset.UtcNow.AddDays(7),
            invitedByUserId,
            eventId);

        await fixture.ApplicationHost.TrackActivity()
            .Timeout(TimeSpan.FromSeconds(10))
            .PublishMessageAndWaitAsync(invitation);

        var sent = fixture.EmailSender.SentMessages.Single(m =>
            string.Equals(m.To, "invitee@example.com", StringComparison.Ordinal));
        Assert.Contains("https://app.test/register/invitation?token=user-token", sent.PlainTextBody, StringComparison.Ordinal);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        var log = await db.NotificationLogs.SingleAsync(l => l.IdempotencyKey == eventId);
        Assert.Equal(invitedByUserId, log.UserId);
        Assert.Equal(NotificationType.UserInvitation, log.NotificationType);
        Assert.Equal(NotificationDeliveryStatus.Sent, log.DeliveryStatus);
    }

    [Fact]
    public async Task OrganizationInvitationCreated_SendsInvitationEmail()
    {
        var eventId = Guid.NewGuid();
        var invitedByUserId = Guid.NewGuid();
        var invitation = new OrganizationInvitationCreatedV1(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "org-invitee@example.com",
            "member",
            "org-token",
            invitedByUserId,
            eventId);

        await fixture.ApplicationHost.TrackActivity()
            .Timeout(TimeSpan.FromSeconds(10))
            .PublishMessageAndWaitAsync(invitation);

        var sent = fixture.EmailSender.SentMessages.Single(m =>
            string.Equals(m.To, "org-invitee@example.com", StringComparison.Ordinal));
        Assert.Contains("https://app.test/register/organization-invitation?token=org-token", sent.PlainTextBody, StringComparison.Ordinal);
        Assert.Contains("member", sent.PlainTextBody, StringComparison.Ordinal);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        var log = await db.NotificationLogs.SingleAsync(l => l.IdempotencyKey == eventId);
        Assert.Equal(invitedByUserId, log.UserId);
        Assert.Equal(NotificationType.OrganizationInvitation, log.NotificationType);
        Assert.Equal(NotificationDeliveryStatus.Sent, log.DeliveryStatus);
    }
}
