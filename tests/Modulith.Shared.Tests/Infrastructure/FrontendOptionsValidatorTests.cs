using Microsoft.Extensions.Options;
using Modulith.Shared.Infrastructure.Frontend;

namespace Modulith.Shared.Tests.Infrastructure;

[Trait("Category", "Unit")]
public sealed class FrontendOptionsValidatorTests
{
    private readonly FrontendOptionsValidator validator = new();

    [Fact]
    public void Validate_WithAbsoluteHttpBaseUrlAndRootedPaths_Succeeds()
    {
        var result = validator.Validate(null, ValidOptions());

        Assert.False(result.Failed);
    }

    [Theory]
    [InlineData("app.example.com")]
    [InlineData("https://app.example.com")]
    [InlineData("ftp://app.example.com")]
    public void Validate_WithInvalidBaseUrl_Fails(string baseUrl)
    {
        var options = new FrontendOptions
        {
            BaseUrl = baseUrl,
            Paths = ValidOptions().Paths,
        };

        var result = validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("Frontend:BaseUrl", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("confirm-email")]
    [InlineData("https://app.example.com/confirm-email")]
    [InlineData("//app.example.com/confirm-email")]
    [InlineData("/confirm-email?token=test")]
    public void Validate_WithInvalidPath_Fails(string path)
    {
        var options = new FrontendOptions
        {
            BaseUrl = "https://app.test",
            Paths = new FrontendPathOptions
            {
                ConfirmEmail = path,
                GoogleConfirm = "/auth/google/confirm",
                ResetPassword = "/reset-password",
            },
        };

        var result = validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("Frontend:Paths:ConfirmEmail", StringComparison.Ordinal));
    }

    private static FrontendOptions ValidOptions() =>
        new()
        {
            BaseUrl = "https://app.test",
            Paths = new FrontendPathOptions
            {
                ConfirmEmail = "/confirm-email",
                GoogleConfirm = "/auth/google/confirm",
                ResetPassword = "/reset-password",
            },
        };
}
