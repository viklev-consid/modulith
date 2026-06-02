using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Modules.Audit.Persistence;
using Modulith.Modules.Catalog.Persistence;
using Modulith.Modules.Notifications.Persistence;
using Modulith.Modules.Organizations.Persistence;
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
/// Injects a <see cref="FlakyEmailSender"/> with <c>throwRetryable: true</c> and
/// <c>clockAdvance = TimeSpan.Zero</c>. The sender throws <see cref="RetryableSmtpException"/>
/// on the first call, which the handler catches and uses to call
/// <c>NotificationSendGuard.MarkReadyAsync</c>, resetting the row to Pending immediately.
/// No clock manipulation is needed — the stale-row path is deliberately excluded to prove that
/// <c>MarkReadyAsync</c> alone drives recovery.
/// </summary>
public sealed class NotificationsRecoveryFixture : ApiTestFixture
{
    internal TestClock Clock { get; } = new TestClock();
    internal FlakyEmailSender FlakyEmail { get; private set; } = null!;

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        // Throw RetryableSmtpException so the handler's catch block fires and calls
        // MarkReadyAsync. No clock advance needed — the stale-row path is excluded.
        FlakyEmail = new FlakyEmailSender(Clock, TimeSpan.Zero, throwRetryable: true);
        services.AddSingleton<IClock>(Clock);
        services.AddSingleton<IEmailSender>(FlakyEmail);
    }

    protected override async Task MigrateAsync(IServiceProvider services)
    {
        await services.GetRequiredService<UsersDbContext>().Database.MigrateAsync();
        await services.GetRequiredService<CatalogDbContext>().Database.MigrateAsync();
        await services.GetRequiredService<AuditDbContext>().Database.MigrateAsync();
        await services.GetRequiredService<OrganizationsDbContext>().Database.MigrateAsync();
        await services.GetRequiredService<NotificationsDbContext>().Database.MigrateAsync();
    }

    protected override string[] GetSchemasToReset() => ["users", "catalog", "audit", "organizations", "notifications"];
}
