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

        // --- Connect ---
        // SslHandshakeException (bad cert, wrong protocol) and AuthenticationException (TLS auth
        // failure) are configuration problems — retrying will not fix them.
        // Network/protocol errors are transient and safe to retry.
        try
        {
            await client.ConnectAsync(_options.Host, _options.Port, secureSocketOptions, ct).ConfigureAwait(false);
        }
        catch (SslHandshakeException ex)
        {
            throw new TerminalSmtpException(
                $"TLS handshake failed connecting to {_options.Host}:{_options.Port}: {ex.Message}", ex);
        }
        catch (AuthenticationException ex)
        {
            // Catches any remaining TLS authentication failure not covered by SslHandshakeException.
            throw new TerminalSmtpException(
                $"TLS authentication failed connecting to {_options.Host}:{_options.Port}: {ex.Message}", ex);
        }
        catch (SmtpCommandException ex) when ((int)ex.StatusCode >= 500)
        {
            throw new TerminalSmtpException(
                $"SMTP server rejected connection permanently ({(int)ex.StatusCode}): {ex.Message}", ex);
        }
        catch (SmtpCommandException ex)
        {
            throw new RetryableSmtpException(
                $"SMTP server returned a transient connect error ({(int)ex.StatusCode}): {ex.Message}", ex);
        }
        catch (SmtpProtocolException ex)
        {
            throw new RetryableSmtpException($"SMTP protocol error during connect: {ex.Message}", ex);
        }
        catch (IOException ex)
        {
            throw new RetryableSmtpException(
                $"Network I/O error connecting to {_options.Host}:{_options.Port}: {ex.Message}", ex);
        }

        // --- Authenticate ---
        // AuthenticationException means wrong credentials or no supported mechanism — terminal config
        // problem. 5xx SmtpCommandException from the auth exchange is also permanent. 4xx is transient.
        if (_options.Username is not null)
        {
            try
            {
                await client.AuthenticateAsync(_options.Username, _options.Password ?? string.Empty, ct).ConfigureAwait(false);
            }
            catch (AuthenticationException ex)
            {
                throw new TerminalSmtpException(
                    $"SMTP authentication failed for '{_options.Username}': {ex.Message}", ex);
            }
            catch (SmtpCommandException ex) when ((int)ex.StatusCode >= 500)
            {
                throw new TerminalSmtpException(
                    $"SMTP server rejected authentication permanently ({(int)ex.StatusCode}): {ex.Message}", ex);
            }
            catch (SmtpCommandException ex)
            {
                throw new RetryableSmtpException(
                    $"SMTP server returned a transient authentication error ({(int)ex.StatusCode}): {ex.Message}", ex);
            }
        }

        // --- Send ---
        // ServiceNotAuthenticatedException here means the server requires auth that was never
        // performed (no Username configured) or that the session has lost its auth state.
        // Both require configuration changes — retrying is pointless.
        try
        {
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
            throw new RetryableSmtpException($"SMTP connection lost before send: {ex.Message}", ex);
        }
        catch (ServiceNotAuthenticatedException ex)
        {
            throw new TerminalSmtpException(
                $"SMTP send rejected: server requires authentication ({ex.Message})", ex);
        }
        catch (IOException ex)
        {
            throw new RetryableSmtpException($"SMTP I/O error during send: {ex.Message}", ex);
        }
    }
}
