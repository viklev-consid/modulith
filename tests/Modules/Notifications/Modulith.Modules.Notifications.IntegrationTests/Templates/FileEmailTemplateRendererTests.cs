using Modulith.Modules.Notifications.Templates;

namespace Modulith.Modules.Notifications.IntegrationTests.Templates;

public sealed class FileEmailTemplateRendererTests
{
    public static TheoryData<EmailTemplateId, object, string> Templates =>
        new()
        {
            { EmailTemplateId.EmailChangeRequest, new EmailChangeRequestModel("token-123", "https://app.example.com/email-change"), "Confirm email address change" },
            { EmailTemplateId.EmailChanged, new EmailChangedModel("alice@example.com"), "Email address changed" },
            { EmailTemplateId.EmailConfirmationRequest, new EmailConfirmationRequestModel("Alice", "token-123", "https://app.example.com/confirm-email"), "Confirm your email address" },
            { EmailTemplateId.OrganizationInvitation, new OrganizationInvitationModel("Admin", "token-123", "https://app.example.com/org-invite"), "Organization invitation" },
            { EmailTemplateId.PasswordChanged, EmptyEmailTemplateModel.Instance, "Password changed" },
            { EmailTemplateId.PasswordResetConfirmation, EmptyEmailTemplateModel.Instance, "Password reset successful" },
            { EmailTemplateId.PasswordResetRequest, new PasswordResetRequestModel("token-123", "https://app.example.com/reset-password"), "Password reset request" },
            { EmailTemplateId.RecoveryCodesRegenerated, EmptyEmailTemplateModel.Instance, "Recovery codes regenerated" },
            { EmailTemplateId.TwoFactorDisabled, EmptyEmailTemplateModel.Instance, "Two-factor authentication disabled" },
            { EmailTemplateId.TwoFactorEnabled, EmptyEmailTemplateModel.Instance, "Two-factor authentication enabled" },
            { EmailTemplateId.UserInvitation, new UserInvitationModel("token-123", "https://app.example.com/invite"), "Accept invitation" },
            { EmailTemplateId.WelcomeEmail, new WelcomeEmailModel("Alice"), "Alice" },
        };

    [Theory]
    [MemberData(nameof(Templates))]
    [Trait("Category", "Integration")]
    public void Render_AllGeneratedTemplates_ReturnsHtml(
        EmailTemplateId templateId,
        object model,
        string expectedHtml)
    {
        var renderer = new FileEmailTemplateRenderer();

        var result = renderer.Render(templateId, model);

        Assert.False(result.IsError);
        Assert.Contains(expectedHtml, result.Value.HtmlBody, StringComparison.Ordinal);
    }

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
        Assert.Contains("Welcome", result.Value.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("Alice", result.Value.HtmlBody, StringComparison.Ordinal);
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
        Assert.Contains("&lt;Alice &amp; Bob&gt;", result.Value.HtmlBody, StringComparison.Ordinal);
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
