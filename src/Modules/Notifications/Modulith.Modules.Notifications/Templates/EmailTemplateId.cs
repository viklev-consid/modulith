namespace Modulith.Modules.Notifications.Templates;

public readonly record struct EmailTemplateId(string Value)
{
    public static EmailTemplateId EmailChangeRequest { get; } = new("users.email-change-request");

    public static EmailTemplateId EmailChanged { get; } = new("users.email-changed");

    public static EmailTemplateId EmailConfirmationRequest { get; } = new("users.email-confirmation-request");

    public static EmailTemplateId OrganizationInvitation { get; } = new("organizations.invitation");

    public static EmailTemplateId PasswordChanged { get; } = new("users.password-changed");

    public static EmailTemplateId PasswordResetConfirmation { get; } = new("users.password-reset-confirmation");

    public static EmailTemplateId PasswordResetRequest { get; } = new("users.password-reset-request");

    public static EmailTemplateId RecoveryCodesRegenerated { get; } = new("users.recovery-codes-regenerated");

    public static EmailTemplateId TwoFactorDisabled { get; } = new("users.two-factor-disabled");

    public static EmailTemplateId TwoFactorEnabled { get; } = new("users.two-factor-enabled");

    public static EmailTemplateId UserInvitation { get; } = new("users.invitation");

    public static EmailTemplateId WelcomeEmail { get; } = new("users.welcome");

    public override string ToString() => Value;
}
