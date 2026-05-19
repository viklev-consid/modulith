using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Modules.Notifications.Domain;
using Modulith.Modules.Notifications.Persistence;
using Modulith.Modules.Users.Contracts.Events;
using Wolverine;
using Wolverine.Tracking;

namespace Modulith.Modules.Notifications.IntegrationTests.Integration;

[Collection("NotificationsCrossModule")]
[Trait("Category", "Integration")]
public sealed class OnUserEmailConfirmedNotificationTests(NotificationsCrossModuleFixture fixture) : IAsyncLifetime
{
    private readonly HttpClient client = fixture.CreateAnonymousClient();

    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task RegisteringUser_DoesNotSendWelcomeEmailBeforeEmailIsConfirmed()
    {
        var request = new { Email = "registration-only@example.com", Password = "Password1!", DisplayName = "Unconfirmed User" };
        HttpResponseMessage? registerResponse = null;

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

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        var welcomeLogExists = await db.NotificationLogs
            .AnyAsync(l => l.UserId == userId && l.NotificationType == NotificationType.WelcomeEmail);

        Assert.False(welcomeLogExists);
        Assert.DoesNotContain(fixture.EmailSender.SentMessages,
            m => string.Equals(m.To, "registration-only@example.com", StringComparison.Ordinal)
                && m.Subject.Contains("Welcome", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task EmailConfirmed_SendsWelcomeEmail_AndLogsNotification()
    {
        var userId = await RegisterUserAsync("notifications-test@example.com", "Notified User");
        var confirmed = new UserEmailConfirmedV1(userId, "notifications-test@example.com", "Notified User", Guid.NewGuid());

        await fixture.ApplicationHost.TrackActivity()
            .Timeout(TimeSpan.FromSeconds(10))
            .PublishMessageAndWaitAsync(confirmed);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        var log = await db.NotificationLogs
            .FirstOrDefaultAsync(l =>
                l.UserId == userId
                && l.NotificationType == NotificationType.WelcomeEmail
                && l.DeliveryStatus == NotificationDeliveryStatus.Sent);

        Assert.NotNull(log);
        Assert.Equal("notifications-test@example.com", log.RecipientEmail);
        Assert.Equal(confirmed.EventId, log.IdempotencyKey);

        var sentEmail = fixture.EmailSender.SentMessages
            .SingleOrDefault(m => string.Equals(m.To, "notifications-test@example.com", StringComparison.Ordinal)
                && m.Subject.Contains("Welcome", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(sentEmail);
        Assert.Contains("Notified User", sentEmail.HtmlBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EmailConfirmedTwice_SendsWelcomeEmailOnlyOnce()
    {
        var userId = await RegisterUserAsync("idempotency-test@example.com", "Idempotent User");
        var eventId = Guid.NewGuid();
        var confirmed = new UserEmailConfirmedV1(userId, "idempotency-test@example.com", "Idempotent User", eventId);

        await fixture.ApplicationHost.TrackActivity()
            .Timeout(TimeSpan.FromSeconds(10))
            .PublishMessageAndWaitAsync(confirmed);

        await fixture.ApplicationHost.TrackActivity()
            .Timeout(TimeSpan.FromSeconds(10))
            .PublishMessageAndWaitAsync(confirmed);

        var welcomeEmailsToUser = fixture.EmailSender.SentMessages
            .Count(m => string.Equals(m.To, "idempotency-test@example.com", StringComparison.Ordinal)
                && m.Subject.Contains("Welcome", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, welcomeEmailsToUser);
    }

    private async Task<Guid> RegisterUserAsync(string email, string displayName)
    {
        var request = new { Email = email, Password = "Password1!", DisplayName = displayName };
        HttpResponseMessage? registerResponse = null;

        Func<IMessageContext, Task> act = async _ =>
        {
            registerResponse = await client.PostAsJsonAsync("/v1/users/register", request);
        };
        await fixture.ApplicationHost.TrackActivity()
            .Timeout(TimeSpan.FromSeconds(10))
            .ExecuteAndWaitAsync(act);

        Assert.Equal(HttpStatusCode.Created, registerResponse!.StatusCode);
        var body = await registerResponse.Content.ReadFromJsonAsync<JsonDocument>();
        return body!.RootElement.GetProperty("userId").GetGuid();
    }
}
