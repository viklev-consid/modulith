using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Notifications.Domain;
using Modulith.Shared.Infrastructure.Persistence;

namespace Modulith.Modules.Notifications.Persistence;

public sealed class NotificationsDbContext(DbContextOptions<NotificationsDbContext> options) : ModuleDbContext(options)
{
    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();
    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("notifications");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationsDbContext).Assembly);
    }
}
