using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Modules.Audit.Persistence;
using Modulith.Modules.Catalog.Persistence;
using Modulith.Modules.Notifications.Persistence;
using Modulith.Modules.Users.Persistence;
using Modulith.Shared.Infrastructure.Notifications;
using Modulith.Shared.Kernel.Interfaces;
using Modulith.TestSupport;
using Modulith.TestSupport.Fakes;

namespace Modulith.Modules.Notifications.IntegrationTests;

[CollectionDefinition("NotificationsRecovery")]
public sealed class NotificationsRecoveryCollection : ICollectionFixture<NotificationsRecoveryFixture> { }

/// <summary>
/// Fixture for Wolverine transient-recovery tests.
/// Injects a <see cref="FlakyEmailSender"/> with <c>clockAdvance = TimeSpan.Zero</c> — no clock
/// manipulation. This proves that <c>NotificationSendGuard.MarkReadyAsync</c> (called by handlers
/// in the <c>catch (IOException)</c> block) is what enables transient recovery, independently of
/// the stale-row path that requires the 5-minute threshold.
/// </summary>
public sealed class NotificationsRecoveryFixture : ApiTestFixture
{
    internal TestClock Clock { get; } = new TestClock();
    internal FlakyEmailSender FlakyEmail { get; private set; } = null!;

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        // Zero clock advance: the stale-row path in TryClaimAsync cannot fire because
        // the Sending row will not be older than StuckSendingThreshold (5 min) on the
        // immediate Wolverine retry. Recovery is entirely driven by MarkReadyAsync.
        FlakyEmail = new FlakyEmailSender(Clock, TimeSpan.Zero);
        services.AddSingleton<IClock>(Clock);
        services.AddSingleton<IEmailSender>(FlakyEmail);
    }

    protected override async Task MigrateAsync(IServiceProvider services)
    {
        await services.GetRequiredService<UsersDbContext>().Database.MigrateAsync();
        await services.GetRequiredService<CatalogDbContext>().Database.MigrateAsync();
        await services.GetRequiredService<AuditDbContext>().Database.MigrateAsync();
        await services.GetRequiredService<NotificationsDbContext>().Database.MigrateAsync();
    }

    protected override string[] GetSchemasToReset() => ["users", "catalog", "audit", "notifications"];
}
