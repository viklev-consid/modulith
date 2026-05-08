using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Modules.Notifications.Persistence;
using Modulith.Modules.Users.Contracts.Events;
using Wolverine;
using Wolverine.Tracking;

namespace Modulith.Modules.Notifications.IntegrationTests.Integration;

[Collection("NotificationsCrossModule")]
[Trait("Category", "Integration")]
public sealed class OnUserRegisteredNotificationTests(NotificationsCrossModuleFixture fixture) : IAsyncLifetime
{
    private readonly HttpClient client = fixture.CreateAnonymousClient();

    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task RegisteringUser_SendsWelcomeEmail_AndLogsNotification()
    {
        // Arrange
        var request = new { Email = "notifications-test@example.com", Password = "Password1!", DisplayName = "Notified User" };
        HttpResponseMessage? registerResponse = null;

        // Act — TrackActivity waits for all cascading messages (including Notifications handler) to finish
        Func<IMessageContext, Task> act = async _ =>
        {
            registerResponse = await client.PostAsJsonAsync("/v1/users/register", request);
        };
        await fixture.ApplicationHost.TrackActivity()
            .Timeout(TimeSpan.FromSeconds(10))
            .ExecuteAndWaitAsync(act);

        Assert.Equal(HttpStatusCode.Created, registerResponse!.StatusCode);
        var body = await registerResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var userId = body!.RootElement.GetProperty("userId").GetGuid();

        // Assert — no polling needed
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        var log = await db.NotificationLogs
            .FirstOrDefaultAsync(l => l.UserId == userId && l.DeliveryStatus == Domain.NotificationDeliveryStatus.Sent);

        Assert.NotNull(log);
        Assert.Equal("notifications-test@example.com", log.RecipientEmail);
        Assert.Equal(Domain.NotificationType.WelcomeEmail, log.NotificationType);
        Assert.Equal(Domain.NotificationDeliveryStatus.Sent, log.DeliveryStatus);

        var sentEmail = fixture.EmailSender.SentMessages
            .SingleOrDefault(m => string.Equals(m.To, "notifications-test@example.com", StringComparison.Ordinal));
        Assert.NotNull(sentEmail);
        Assert.Contains("Welcome", sentEmail.Subject, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Notified User", sentEmail.HtmlBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RegisteringUserTwice_SendsWelcomeEmailOnlyOnce()
    {
        // Wolverine retries can trigger the handler multiple times; the idempotency guard must prevent duplicate emails.
        var request = new { Email = "idempotency-test@example.com", Password = "Password1!", DisplayName = "Idempotent User" };
        HttpResponseMessage? registerResponse = null;

        // First delivery — via HTTP registration (also sets up consent for the user).
        Func<IMessageContext, Task> act = async _ =>
        {
            registerResponse = await client.PostAsJsonAsync("/v1/users/register", request);
        };
        await fixture.ApplicationHost.TrackActivity()
            .Timeout(TimeSpan.FromSeconds(10))
            .ExecuteAndWaitAsync(act);

        Assert.Equal(HttpStatusCode.Created, registerResponse!.StatusCode);
        var body = await registerResponse!.Content.ReadFromJsonAsync<JsonDocument>();
        var userId = body!.RootElement.GetProperty("userId").GetGuid();

        // Retrieve the EventId that the notification handler persisted as the idempotency key.
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        var log = await db.NotificationLogs.FirstAsync(l => l.UserId == userId);
        var originalEventId = log.IdempotencyKey;

        // Second delivery — same event with the same EventId, simulating a Wolverine retry.
        var redelivery = new UserRegisteredV1(userId, "idempotency-test@example.com", "Idempotent User", originalEventId);
        await fixture.ApplicationHost.TrackActivity()
            .Timeout(TimeSpan.FromSeconds(10))
            .PublishMessageAndWaitAsync(redelivery);

        // The idempotency guard must have suppressed the second send.
        var emailsToUser = fixture.EmailSender.SentMessages
            .Count(m => string.Equals(m.To, "idempotency-test@example.com", StringComparison.Ordinal));
        Assert.Equal(1, emailsToUser);
    }
}
