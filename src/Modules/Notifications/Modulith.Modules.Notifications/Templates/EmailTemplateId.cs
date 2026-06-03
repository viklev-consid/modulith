namespace Modulith.Modules.Notifications.Templates;

public readonly record struct EmailTemplateId(string Value)
{
    public static EmailTemplateId WelcomeEmail { get; } = new("users.welcome");

    public override string ToString() => Value;
}
