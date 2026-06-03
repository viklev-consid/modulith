namespace Modulith.Modules.Notifications.Templates;

public sealed record EmptyEmailTemplateModel
{
    public static EmptyEmailTemplateModel Instance { get; } = new();

    private EmptyEmailTemplateModel()
    {
    }
}
