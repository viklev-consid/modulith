using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Modulith.Api.Infrastructure.Scheduling;
using Modulith.MigrationService;
using Modulith.Modules.Audit.Persistence;
using Modulith.Modules.Catalog.Persistence;
using Modulith.Modules.Notifications.Persistence;
using Modulith.Modules.Organizations.Persistence;
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

builder.Services.AddDbContext<OrganizationsDbContext>(opts =>
    opts.UseNpgsql(
        connectionString,
        npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "organizations")));

builder.Services.AddDbContext<TickerQOperationalDbContext>(opts =>
    opts.UseNpgsql(
        connectionString,
        npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", TickerQOperationalDbContext.Schema)));

using var host = builder.Build();

await using var scope = host.Services.CreateAsyncScope();
var logger = scope.ServiceProvider
    .GetRequiredService<ILoggerFactory>()
    .CreateLogger("Modulith.MigrationService");

EnsureAllModuleDbContextsAreRegistered(scope.ServiceProvider);

await MigrateAsync<UsersDbContext>(scope.ServiceProvider, logger);
await MigrateAsync<CatalogDbContext>(scope.ServiceProvider, logger);
await MigrateAsync<AuditDbContext>(scope.ServiceProvider, logger);
await MigrateAsync<NotificationsDbContext>(scope.ServiceProvider, logger);
await MigrateAsync<OrganizationsDbContext>(scope.ServiceProvider, logger);
await MigrateAsync<TickerQOperationalDbContext>(scope.ServiceProvider, logger);

MigrationLog.Completed(logger);

static async Task MigrateAsync<TDbContext>(IServiceProvider services, ILogger logger)
    where TDbContext : DbContext
{
    var db = services.GetRequiredService<TDbContext>();
    MigrationLog.Applying(logger, typeof(TDbContext).Name);
    await db.Database.MigrateAsync();
}

static void EnsureAllModuleDbContextsAreRegistered(IServiceProvider services)
{
    var missing = DiscoverModuleDbContexts()
        .Where(type => services.GetService(type) is null)
        .Select(type => type.FullName)
        .Order(StringComparer.Ordinal)
        .ToArray();

    if (missing.Length > 0)
    {
        throw new InvalidOperationException(
            $"Module DbContexts are missing from the migration plan: {string.Join(", ", missing)}.");
    }
}

static IEnumerable<Type> DiscoverModuleDbContexts()
{
    const string moduleAssemblyPrefix = "Modulith.Modules.";
    const string contractsAssemblySuffix = ".Contracts";

    return Directory
        .EnumerateFiles(AppContext.BaseDirectory, $"{moduleAssemblyPrefix}*.dll")
        .Select(AssemblyName.GetAssemblyName)
        .Where(name => name.Name is { } assemblyName
            && !assemblyName.EndsWith(contractsAssemblySuffix, StringComparison.Ordinal))
        .Select(Assembly.Load)
        .SelectMany(GetLoadableTypes)
        .Where(type => type is { IsAbstract: false }
            && type.IsAssignableTo(typeof(DbContext)));
}

static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
{
    try
    {
        return assembly.GetTypes();
    }
    catch (ReflectionTypeLoadException ex)
    {
        return ex.Types.Where(type => type is not null)!;
    }
}
