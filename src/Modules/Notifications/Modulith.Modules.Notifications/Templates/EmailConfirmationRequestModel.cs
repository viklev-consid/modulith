namespace Modulith.Modules.Notifications.Templates;

public sealed record EmailConfirmationRequestModel(
    string DisplayName,
    string Token,
    string ConfirmationUrl);
