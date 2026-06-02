using Microsoft.Extensions.Options;

namespace Modulith.Shared.Infrastructure.Notifications;

public sealed class SmtpOptionsValidator : IValidateOptions<SmtpOptions>
{
    public ValidateOptionsResult Validate(string? name, SmtpOptions options) =>
        options.UseSsl && options.UseStartTls
            ? ValidateOptionsResult.Fail("SMTP cannot use implicit TLS and STARTTLS at the same time.")
            : ValidateOptionsResult.Success;
}
