using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Notifications.Mapping;
using Modulith.Modules.Notifications.Persistence;
using Modulith.Modules.Notifications.Policies;

namespace Modulith.Modules.Notifications.Features.GetMyNotificationPreferences;

public sealed class GetMyNotificationPreferencesHandler(NotificationsDbContext db)
{
    public async Task<ErrorOr<GetMyNotificationPreferencesResponse>> Handle(
        GetMyNotificationPreferencesQuery query,
        CancellationToken ct)
    {
        var preferences = await db.NotificationPreferences
            .AsNoTracking()
            .Where(p => p.UserId == query.UserId)
            .ToDictionaryAsync(p => p.Category, ct);

        return new GetMyNotificationPreferencesResponse(
            NotificationPreferenceDefaults.All.Select(defaults =>
            {
                preferences.TryGetValue(defaults.Category, out var stored);
                return new MyNotificationPreferenceResponse(
                    defaults.Category.ToContract(),
                    stored?.BellEnabled ?? defaults.BellEnabled,
                    stored?.EmailEnabled ?? defaults.EmailEnabled,
                    defaults.IsLocked);
            }).ToList());
    }
}
