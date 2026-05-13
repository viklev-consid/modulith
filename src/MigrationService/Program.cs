using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Modulith.MigrationService;
using Modulith.Api.Infrastructure.Scheduling;
using Modulith.Modules.Audit.Persistence;
using Modulith.Modules.Catalog.Persistence;
using Modulith.Modules.Notifications.Persistence;
using Modulith.Modules.Users.Persistence;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

var connectionString = builder.Configuration.GetConnectionString("db")
    ?? throw new InvalidOperationException("Connection string 'db' was not found.");

builder.Services.AddDbContext<UsersDbContext>(opts =>
    opts.UseNpgsql(
        connectionString,
        npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "users")));

builder.Services.AddDbContext<CatalogDbContext>(opts =>
    opts.UseNpgsql(
        connectionString,
        npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "catalog")));

builder.Services.AddDbContext<AuditDbContext>(opts =>
    opts.UseNpgsql(
        connectionString,
        npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "audit")));

builder.Services.AddDbContext<NotificationsDbContext>(opts =>
    opts.UseNpgsql(
        connectionString,
        npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "notifications")));

builder.Services.AddDbContext<TickerQOperationalDbContext>(opts =>
    opts.UseNpgsql(
        connectionString,
        npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", TickerQOperationalDbContext.Schema)));

using var host = builder.Build();

await using var scope = host.Services.CreateAsyncScope();
var logger = scope.ServiceProvider
    .GetRequiredService<ILoggerFactory>()
    .CreateLogger("Modulith.MigrationService");

await MigrateAsync<UsersDbContext>(scope.ServiceProvider, logger);
await MigrateAsync<CatalogDbContext>(scope.ServiceProvider, logger);
await MigrateAsync<AuditDbContext>(scope.ServiceProvider, logger);
await MigrateAsync<NotificationsDbContext>(scope.ServiceProvider, logger);
await MigrateAsync<TickerQOperationalDbContext>(scope.ServiceProvider, logger);

MigrationLog.Completed(logger);

static async Task MigrateAsync<TDbContext>(IServiceProvider services, ILogger logger)
    where TDbContext : DbContext
{
    var db = services.GetRequiredService<TDbContext>();
    MigrationLog.Applying(logger, typeof(TDbContext).Name);
    await db.Database.MigrateAsync();
}
