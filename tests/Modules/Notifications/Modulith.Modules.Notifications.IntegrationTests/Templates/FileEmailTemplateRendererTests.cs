using Modulith.Modules.Notifications.Templates;

namespace Modulith.Modules.Notifications.IntegrationTests.Templates;

public sealed class FileEmailTemplateRendererTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void Render_WithRequiredValues_ReturnsSubjectAndHtml()
    {
        var renderer = new FileEmailTemplateRenderer();

        var result = renderer.Render(
            EmailTemplateId.WelcomeEmail,
            new WelcomeEmailModel("Alice"));

        Assert.False(result.IsError);
        Assert.Equal("Welcome to Modulith!", result.Value.Subject);
        Assert.Contains("Welcome, Alice!", result.Value.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Render_EncodesStringValues()
    {
        var renderer = new FileEmailTemplateRenderer();

        var result = renderer.Render(
            EmailTemplateId.WelcomeEmail,
            new WelcomeEmailModel("<Alice & Bob>"));

        Assert.False(result.IsError);
        Assert.Contains("Welcome, &lt;Alice &amp; Bob&gt;!", result.Value.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Welcome, <Alice & Bob>!", result.Value.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Render_WithMissingRequiredValue_ReturnsValidationError()
    {
        var renderer = new FileEmailTemplateRenderer();

        var result = renderer.Render(
            EmailTemplateId.WelcomeEmail,
            new Dictionary<string, object?>(StringComparer.Ordinal));

        Assert.True(result.IsError);
        Assert.Contains(result.Errors, error => string.Equals(error.Code, "Notifications.EmailTemplate", StringComparison.Ordinal));
    }
}
