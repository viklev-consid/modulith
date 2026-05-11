using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Notifications.Domain;
using Modulith.Modules.Notifications.Errors;
using Modulith.Modules.Notifications.Mapping;
using Modulith.Modules.Notifications.Persistence;
using Modulith.Modules.Notifications.Policies;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Notifications.Features.UpdateMyNotificationPreferences;

public sealed class UpdateMyNotificationPreferencesHandler(NotificationsDbContext db, IClock clock)
{
    public async Task<ErrorOr<Success>> Handle(UpdateMyNotificationPreferencesCommand command, CancellationToken ct)
    {
        var requested = command.Preferences
            .GroupBy(p => p.Category)
            .Select(g => g.Last())
            .ToList();

        var stored = await db.NotificationPreferences
            .Where(p => p.UserId == command.UserId)
            .ToDictionaryAsync(p => p.Category, ct);

        foreach (var preference in requested)
        {
            var category = preference.Category.ToDomain();
            var defaults = NotificationPreferenceDefaults.Get(category);

            if (defaults.IsLocked && (preference.BellEnabled != defaults.BellEnabled || preference.EmailEnabled != defaults.EmailEnabled))
            {
                return NotificationsErrors.NotificationPreferenceInvalid;
            }

            if (stored.TryGetValue(category, out var existing))
            {
                existing.Update(preference.BellEnabled, preference.EmailEnabled, clock.UtcNow);
            }
            else
            {
                db.NotificationPreferences.Add(NotificationPreference.Create(
                    command.UserId,
                    category,
                    preference.BellEnabled,
                    preference.EmailEnabled,
                    clock.UtcNow));
            }
        }

        await db.SaveChangesAsync(ct);
        return Result.Success;
    }
}
