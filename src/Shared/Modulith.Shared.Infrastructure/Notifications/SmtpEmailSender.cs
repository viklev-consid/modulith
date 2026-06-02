using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Modulith.Shared.Infrastructure.Notifications;

public sealed class SmtpEmailSender(IOptions<SmtpOptions> options) : IEmailSender
{
    private readonly SmtpOptions options = options.Value;

    public async Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        if (options.UseSsl && options.UseStartTls)
        {
            throw new TerminalSmtpException("SMTP cannot use implicit TLS and STARTTLS at the same time.");
        }

        if (!options.UseSsl && !options.UseStartTls && (options.Username is not null || options.Password is not null))
        {
            throw new TerminalSmtpException("SMTP credentials require TLS.");
        }

        if (!options.UseSsl && !options.UseStartTls && !options.AllowInsecureTransport)
        {
            throw new TerminalSmtpException("SMTP TLS is required unless insecure transport is explicitly allowed.");
        }

        var mail = new MimeMessage();
        mail.From.Add(MailboxAddress.Parse(message.From ?? options.DefaultFrom));
        mail.To.Add(MailboxAddress.Parse(message.To));
        mail.Subject = message.Subject;

        var builder = new BodyBuilder
        {
            HtmlBody = message.HtmlBody,
            TextBody = message.PlainTextBody,
        };
        mail.Body = builder.ToMessageBody();

        using var client = new SmtpClient();

        var secureSocketOptions = SecureSocketOptions.None;
        if (options.UseSsl)
        {
            secureSocketOptions = SecureSocketOptions.SslOnConnect;
        }
        else if (options.UseStartTls)
        {
            secureSocketOptions = SecureSocketOptions.StartTls;
        }

        // --- Connect ---
        // SslHandshakeException (bad cert, wrong protocol) and AuthenticationException (TLS auth
        // failure) are configuration problems — retrying will not fix them.
        // Network/protocol errors are transient and safe to retry.
        try
        {
            await client.ConnectAsync(options.Host, options.Port, secureSocketOptions, ct).ConfigureAwait(false);
        }
        catch (SslHandshakeException ex)
        {
            throw new TerminalSmtpException(
                $"TLS handshake failed connecting to {options.Host}:{options.Port}: {ex.Message}", ex);
        }
        catch (AuthenticationException ex)
        {
            // Catches any remaining TLS authentication failure not covered by SslHandshakeException.
            throw new TerminalSmtpException(
                $"TLS authentication failed connecting to {options.Host}:{options.Port}: {ex.Message}", ex);
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
                $"Network I/O error connecting to {options.Host}:{options.Port}: {ex.Message}", ex);
        }

        // --- Authenticate ---
        // AuthenticationException means wrong credentials or no supported mechanism — terminal config
        // problem. 5xx SmtpCommandException from the auth exchange is also permanent. 4xx is transient.
        if (options.Username is not null)
        {
            try
            {
                await client.AuthenticateAsync(options.Username, options.Password ?? string.Empty, ct).ConfigureAwait(false);
            }
            catch (AuthenticationException ex)
            {
                throw new TerminalSmtpException(
                    $"SMTP authentication failed for '{options.Username}': {ex.Message}", ex);
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
        // DisconnectAsync runs in a finally so the SMTP QUIT is sent even when SendAsync throws.
        // Failures in DisconnectAsync are swallowed — the using statement disposes the socket.
        try
        {
            await client.SendAsync(mail, ct).ConfigureAwait(false);
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
        finally
        {
            try { await client.DisconnectAsync(quit: true, CancellationToken.None).ConfigureAwait(false); }
            catch { /* socket cleanup handled by using statement */ }
        }
    }
}
