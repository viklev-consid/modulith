using Modulith.Shared.Infrastructure.Http;

namespace Modulith.Shared.Tests.Infrastructure;

[Trait("Category", "Unit")]
public sealed class CorsOptionsValidatorTests
{
    private readonly CorsOptionsValidator validator = new();

    [Fact]
    public void Validate_WithHttpOrigins_Succeeds()
    {
        var result = validator.Validate(null, new CorsOptions
        {
            PolicyName = "BrowserClients",
            AllowedOrigins = ["http://localhost:3000", "https://app.test"],
            AllowCredentials = true,
        });

        Assert.False(result.Failed);
    }

    [Theory]
    [InlineData("https://app.example.com/path")]
    [InlineData("https://app.example.com?x=1")]
    [InlineData("https://app.example.com/")]
    [InlineData("https://app.example.com")]
    [InlineData("ftp://app.example.com")]
    public void Validate_WithNonOriginValue_Fails(string origin)
    {
        var result = validator.Validate(null, new CorsOptions
        {
            PolicyName = "BrowserClients",
            AllowedOrigins = [origin],
        });

        Assert.True(result.Failed);
    }

    [Fact]
    public void Validate_WithWildcardAndCredentials_Fails()
    {
        var result = validator.Validate(null, new CorsOptions
        {
            PolicyName = "BrowserClients",
            AllowedOrigins = ["*"],
            AllowCredentials = true,
        });

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("cannot contain '*'", StringComparison.Ordinal));
    }
}
