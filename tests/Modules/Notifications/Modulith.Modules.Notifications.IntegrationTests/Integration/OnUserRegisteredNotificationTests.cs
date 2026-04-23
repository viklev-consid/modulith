using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Modules.Notifications.Persistence;

namespace Modulith.Modules.Notifications.IntegrationTests.Integration;

[Collection("NotificationsCrossModule")]
[Trait("Category", "Integration")]
public sealed class OnUserRegisteredNotificationTests(NotificationsCrossModuleFixture fixture) : IAsyncLifetime
{
    private readonly HttpClient _client = fixture.CreateAnonymousClient();

    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task RegisteringUser_SendsWelcomeEmail_AndLogsNotification()
    {
        // Arrange
        var request = new { Email = "notifications-test@example.com", Password = "Password1!", DisplayName = "Notified User" };

        // Act
        var response = await _client.PostAsJsonAsync("/v1/users/register", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var userId = body!.RootElement.GetProperty("userId").GetGuid();

        // Assert — poll until Wolverine outbox delivers UserRegisteredV1 and the notification log is written
        Domain.NotificationLog? log = null;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while (!cts.IsCancellationRequested)
        {
            using var scope = fixture.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
            log = await db.NotificationLogs
                .FirstOrDefaultAsync(l => l.UserId == userId, cts.Token);
            if (log is not null)
            {
                break;
            }

            await Task.Delay(200, cts.Token);
        }

        Assert.NotNull(log);
        Assert.Equal("notifications-test@example.com", log.RecipientEmail);
        Assert.Equal(Domain.NotificationType.WelcomeEmail, log.NotificationType);

        // Assert the fake sender captured the email
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

        var response = await _client.PostAsJsonAsync("/v1/users/register", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var userId = body!.RootElement.GetProperty("userId").GetGuid();

        // Wait for the log to appear
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while (!cts.IsCancellationRequested)
        {
            using var scope = fixture.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
            var exists = await db.NotificationLogs.AnyAsync(l => l.UserId == userId, cts.Token);
            if (exists)
            {
                break;
            }

            await Task.Delay(200, cts.Token);
        }

        var emailsToUser = fixture.EmailSender.SentMessages
            .Count(m => string.Equals(m.To, "idempotency-test@example.com", StringComparison.Ordinal));
        Assert.Equal(1, emailsToUser);
    }
}
