using Microsoft.Extensions.Options;
using Modulith.Shared.Infrastructure.Frontend;

namespace Modulith.Shared.Tests.Infrastructure;

[Trait("Category", "Unit")]
public sealed class FrontendUrlBuilderTests
{
    [Fact]
    public void ConfirmEmail_BuildsConfiguredUrl()
    {
        var builder = CreateBuilder("https://app.test");

        var url = builder.ConfirmEmail("email-token");

        Assert.Equal("https://app.test/confirm-email?token=email-token", url);
    }

    [Fact]
    public void ConfirmEmailChange_BuildsConfiguredUrl()
    {
        var builder = CreateBuilder("https://app.test");

        var url = builder.ConfirmEmailChange("email-token");

        Assert.Equal("https://app.test/confirm-email-change?token=email-token", url);
    }

    [Fact]
    public void ResetPassword_BuildsConfiguredUrl()
    {
        var builder = CreateBuilder("https://app.test");

        var url = builder.ResetPassword("reset-token");

        Assert.Equal("https://app.test/reset-password?token=reset-token", url);
    }

    [Fact]
    public void Links_EncodeQueryValues()
    {
        var builder = CreateBuilder("https://app.test");

        var url = builder.ResetPassword("token with symbols /+");

        Assert.Equal("https://app.test/reset-password?token=token%20with%20symbols%20%2F%2B", url);
    }

    [Fact]
    public void Links_DoNotDuplicateSlash_WhenBaseUrlHasTrailingSlash()
    {
        var builder = CreateBuilder("https://app.test/");

        var url = builder.ConfirmEmailChange("token");

        Assert.Equal("https://app.test/confirm-email-change?token=token", url);
    }

    private static FrontendUrlBuilder CreateBuilder(string baseUrl) =>
        new(Options.Create(new FrontendOptions
        {
            BaseUrl = baseUrl,
            Paths = new FrontendPathOptions
            {
                ConfirmEmail = "/confirm-email",
                ConfirmEmailChange = "/confirm-email-change",
                ResetPassword = "/reset-password",
            },
        }));
}
