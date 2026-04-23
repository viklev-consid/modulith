using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Modulith.Shared.Infrastructure.Notifications;

public sealed class SmtpEmailSender(IOptions<SmtpOptions> options) : IEmailSender
{
    private readonly SmtpOptions _options = options.Value;

    public async Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        var mail = new MimeMessage();
        mail.From.Add(MailboxAddress.Parse(message.From ?? _options.DefaultFrom));
        mail.To.Add(MailboxAddress.Parse(message.To));
        mail.Subject = message.Subject;

        var builder = new BodyBuilder
        {
            HtmlBody = message.HtmlBody,
            TextBody = message.PlainTextBody,
        };
        mail.Body = builder.ToMessageBody();

        using var client = new SmtpClient();

        var secureSocketOptions = _options.UseSsl
            ? SecureSocketOptions.SslOnConnect
            : SecureSocketOptions.None;

        await client.ConnectAsync(_options.Host, _options.Port, secureSocketOptions, ct).ConfigureAwait(false);

        if (_options.Username is not null)
        {
            await client.AuthenticateAsync(_options.Username, _options.Password ?? string.Empty, ct).ConfigureAwait(false);
        }

        await client.SendAsync(mail, ct).ConfigureAwait(false);
        await client.DisconnectAsync(quit: true, ct).ConfigureAwait(false);
    }
}
