using Modulith.Modules.Notifications.Contracts.Dtos;

namespace Modulith.Modules.Notifications.Features.GetMyNotificationPreferences;

public sealed record GetMyNotificationPreferencesResponse(IReadOnlyList<MyNotificationPreferenceResponse> Preferences);

public sealed record MyNotificationPreferenceResponse(
    NotificationCategory Category,
    bool BellEnabled,
    bool EmailEnabled,
    bool IsLocked);
