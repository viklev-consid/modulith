using Microsoft.Extensions.Options;
using Modulith.Shared.Infrastructure.Notifications;

namespace Modulith.Shared.Tests.Infrastructure;

[Trait("Category", "Unit")]
public sealed class SmtpEmailSenderTests
{
    [Fact]
    public async Task SendAsync_WithNoTls_ReturnsTerminalFailureBeforeConnecting()
    {
        var sender = new SmtpEmailSender(Options.Create(new SmtpOptions
        {
            Host = "127.0.0.1",
            Port = 1,
            UseSsl = false,
        }));
        var message = new EmailMessage("to@example.com", "subject", "body", "body");

        var exception = await Assert.ThrowsAsync<TerminalSmtpException>(
            () => sender.SendAsync(message, CancellationToken.None));

        Assert.Equal("SMTP TLS is required unless insecure transport is explicitly allowed.", exception.Message);
    }

    [Fact]
    public async Task SendAsync_WithCredentialsAndInsecureTransport_ReturnsTerminalFailureBeforeConnecting()
    {
        var sender = new SmtpEmailSender(Options.Create(new SmtpOptions
        {
            Host = "127.0.0.1",
            Port = 1,
            AllowInsecureTransport = true,
            Username = "user",
            Password = "secret",
        }));
        var message = new EmailMessage("to@example.com", "subject", "body", "body");

        var exception = await Assert.ThrowsAsync<TerminalSmtpException>(
            () => sender.SendAsync(message, CancellationToken.None));

        Assert.Equal("SMTP credentials require TLS.", exception.Message);
    }
}
