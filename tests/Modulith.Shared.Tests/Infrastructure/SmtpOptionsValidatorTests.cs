using Modulith.Shared.Infrastructure.Notifications;

namespace Modulith.Shared.Tests.Infrastructure;

[Trait("Category", "Unit")]
public sealed class SmtpOptionsValidatorTests
{
    private readonly SmtpOptionsValidator validator = new();

    [Fact]
    public void Validate_WithImplicitTlsAndStartTls_ReturnsFailure()
    {
        var result = validator.Validate(null, new SmtpOptions
        {
            UseSsl = true,
            UseStartTls = true,
        });

        Assert.True(result.Failed);
        Assert.Equal(
            ["SMTP cannot use implicit TLS and STARTTLS at the same time."],
            result.Failures);
    }

    [Fact]
    public void Validate_WithStartTlsOnly_ReturnsSuccess()
    {
        var result = validator.Validate(null, new SmtpOptions
        {
            UseStartTls = true,
        });

        Assert.True(result.Succeeded);
    }
}
