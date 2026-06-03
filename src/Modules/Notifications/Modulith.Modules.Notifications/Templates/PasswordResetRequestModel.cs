namespace Modulith.Modules.Notifications.Templates;

public sealed record PasswordResetRequestModel(
    string Token,
    string ResetUrl);
