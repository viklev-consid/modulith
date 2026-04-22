namespace Modulith.Modules.Notifications.Contracts.Authorization;

public static class NotificationsPermissions
{
    public const string TemplatesRead  = "notifications.templates.read";
    public const string TemplatesWrite = "notifications.templates.write";

    public static IReadOnlyCollection<string> All { get; } =
        [TemplatesRead, TemplatesWrite];
}
