using MailKit;
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

        try
        {
            await client.ConnectAsync(_options.Host, _options.Port, secureSocketOptions, ct).ConfigureAwait(false);

            if (_options.Username is not null)
            {
                await client.AuthenticateAsync(_options.Username, _options.Password ?? string.Empty, ct).ConfigureAwait(false);
            }

            await client.SendAsync(mail, ct).ConfigureAwait(false);
            await client.DisconnectAsync(quit: true, ct).ConfigureAwait(false);
        }
        catch (SmtpCommandException ex) when ((int)ex.StatusCode >= 500)
        {
            // 5xx permanent server rejection — retrying will not succeed.
            throw new TerminalSmtpException(
                $"SMTP server rejected the message permanently ({(int)ex.StatusCode}): {ex.Message}", ex);
        }
        catch (SmtpCommandException ex)
        {
            // 4xx transient server rejection — safe to retry.
            throw new RetryableSmtpException(
                $"SMTP server returned a transient error ({(int)ex.StatusCode}): {ex.Message}", ex);
        }
        catch (SmtpProtocolException ex)
        {
            throw new RetryableSmtpException($"SMTP protocol error: {ex.Message}", ex);
        }
        catch (ServiceNotConnectedException ex)
        {
            throw new RetryableSmtpException($"SMTP service not connected: {ex.Message}", ex);
        }
        catch (ServiceNotAuthenticatedException ex)
        {
            throw new RetryableSmtpException($"SMTP service not authenticated: {ex.Message}", ex);
        }
        catch (IOException ex)
        {
            throw new RetryableSmtpException($"SMTP I/O error: {ex.Message}", ex);
        }
    }
}
