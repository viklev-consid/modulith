using Modulith.Modules.Users.Contracts.Events;
using Wolverine.Tracking;

namespace Modulith.Modules.Notifications.IntegrationTests.Integration;

[Collection("NotificationsCrossModule")]
[Trait("Category", "Integration")]
public sealed class OnExternalLoginPendingNotificationTests(NotificationsCrossModuleFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ExternalLoginPending_ForNewUser_SendsConfirmationLinkAndFallbackToken()
    {
        const string email = "new-google-confirm-link@example.com";
        const string token = "token with symbols /+";

        var message = new ExternalLoginPendingV1(
            Provider: "Google",
            Email: email,
            DisplayName: "New Google User",
            IsExistingUser: false,
            RawToken: token,
            EventId: Guid.NewGuid());

        await fixture.ApplicationHost.TrackActivity()
            .Timeout(TimeSpan.FromSeconds(10))
            .PublishMessageAndWaitAsync(message);

        var sentEmail = fixture.EmailSender.SentMessages
            .Single(m => string.Equals(m.To, email, StringComparison.Ordinal));

        Assert.Equal("Confirm your new account", sentEmail.Subject);
        Assert.Contains("Continue creating your account", sentEmail.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("https://app.test/auth/google/confirm?token=token%20with%20symbols%20%2F%2B", sentEmail.HtmlBody, StringComparison.Ordinal);
        Assert.Contains(token, sentEmail.PlainTextBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExternalLoginPending_ForExistingUser_SendsLinkConfirmationLinkAndFallbackToken()
    {
        const string email = "existing-google-confirm-link@example.com";
        const string token = "existing-token";

        var message = new ExternalLoginPendingV1(
            Provider: "Google",
            Email: email,
            DisplayName: "Existing User",
            IsExistingUser: true,
            RawToken: token,
            EventId: Guid.NewGuid());

        await fixture.ApplicationHost.TrackActivity()
            .Timeout(TimeSpan.FromSeconds(10))
            .PublishMessageAndWaitAsync(message);

        var sentEmail = fixture.EmailSender.SentMessages
            .Single(m => string.Equals(m.To, email, StringComparison.Ordinal));

        Assert.Equal("Confirm linking your Google account", sentEmail.Subject);
        Assert.Contains("Confirm Google account link", sentEmail.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("https://app.test/auth/google/confirm?token=existing-token", sentEmail.HtmlBody, StringComparison.Ordinal);
        Assert.Contains(token, sentEmail.PlainTextBody, StringComparison.Ordinal);
    }
}
