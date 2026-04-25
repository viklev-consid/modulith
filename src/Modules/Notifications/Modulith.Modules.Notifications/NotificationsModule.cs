using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using Modulith.Modules.Notifications.Gdpr;
using Modulith.Modules.Notifications.Integration.Subscribers;
using Modulith.Modules.Notifications.Persistence;
using Modulith.Modules.Notifications.Contracts.Authorization;
using Modulith.Shared.Infrastructure.Authorization;
using Modulith.Shared.Infrastructure.Notifications;
using Modulith.Shared.Infrastructure.Persistence;
using Modulith.Shared.Kernel.Interfaces;
using Wolverine;

namespace Modulith.Modules.Notifications;

public static class NotificationsModule
{
    public static IServiceCollection AddNotificationsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<SmtpOptions>()
            .Bind(configuration.GetSection("Modules:Notifications:Smtp"))
            .ValidateOnStart();

        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddPermissions(NotificationsPermissions.All);

        services.AddScoped<AuditableEntitySaveChangesInterceptor>();

        services.AddDbContext<NotificationsDbContext>((sp, opts) =>
        {
            opts.UseNpgsql(
                configuration.GetConnectionString("db"),
                b => b.MigrationsHistoryTable("__ef_migrations_history", "notifications"));
            opts.AddInterceptors(sp.GetRequiredService<AuditableEntitySaveChangesInterceptor>());
        });

        services.AddScoped<IPersonalDataExporter, NotificationsPersonalDataExporter>();
        services.AddScoped<IPersonalDataEraser, NotificationsPersonalDataEraser>();

        services.AddHealthChecks()
            .AddDbContextCheck<NotificationsDbContext>("notifications-db", tags: ["ready"]);

        services.AddOpenTelemetry()
            .WithTracing(t => t.AddSource(NotificationsTelemetry.SourceName))
            .WithMetrics(m => m.AddMeter(NotificationsTelemetry.MeterName));

        return services;
    }

    public static WolverineOptions AddNotificationsHandlers(this WolverineOptions opts)
    {
        opts.Discovery.IncludeType<OnUserRegisteredHandler>();
        opts.Discovery.IncludeType<OnPasswordResetRequestedHandler>();
        opts.Discovery.IncludeType<OnPasswordResetHandler>();
        opts.Discovery.IncludeType<OnPasswordChangedHandler>();
        opts.Discovery.IncludeType<OnEmailChangeRequestedHandler>();
        opts.Discovery.IncludeType<OnEmailChangedHandler>();

        // External login notifications — Phase 14
        opts.Discovery.IncludeType<OnExternalLoginPendingHandler>();
        opts.Discovery.IncludeType<OnExternalLoginLinkedHandler>();
        opts.Discovery.IncludeType<OnExternalLoginUnlinkedHandler>();
        return opts;
    }

    public static IEndpointRouteBuilder MapNotificationsEndpoints(this IEndpointRouteBuilder app)
    {
        // No public endpoints for Notifications in Phase 7.
        return app;
    }
}
