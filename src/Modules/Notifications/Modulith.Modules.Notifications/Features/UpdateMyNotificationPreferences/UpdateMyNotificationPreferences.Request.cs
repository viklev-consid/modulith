using Modulith.Modules.Notifications.Contracts.Dtos;

namespace Modulith.Modules.Notifications.Features.UpdateMyNotificationPreferences;

public sealed record UpdateMyNotificationPreferencesRequest(IReadOnlyList<UpdateMyNotificationPreferenceRequest> Preferences);

public sealed record UpdateMyNotificationPreferenceRequest(
    NotificationCategory Category,
    bool BellEnabled,
    bool EmailEnabled);
