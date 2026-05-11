using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Modules.Notifications.Contracts.Authorization;
using Modulith.Modules.Notifications.Features.ArchiveNotification;
using Modulith.Modules.Notifications.Features.CreateNotification;
using Modulith.Modules.Notifications.Features.GetMyNotificationPreferences;
using Modulith.Modules.Notifications.Features.GetUnreadNotificationCount;
using Modulith.Modules.Notifications.Features.ListMyNotifications;
using Modulith.Modules.Notifications.Features.MarkAllNotificationsAsRead;
using Modulith.Modules.Notifications.Features.MarkNotificationAsRead;
using Modulith.Modules.Notifications.Features.StreamMyNotifications;
using Modulith.Modules.Notifications.Features.UpdateMyNotificationPreferences;
using Modulith.Modules.Notifications.Gdpr;
using Modulith.Modules.Notifications.Integration.Subscribers;
using Modulith.Modules.Notifications.Jobs;
using Modulith.Modules.Notifications.Persistence;
using Modulith.Modules.Notifications.Policies;
using Modulith.Modules.Notifications.Streaming;
using Modulith.Shared.Infrastructure.Authorization;
using Modulith.Shared.Infrastructure.Notifications;
using Modulith.Shared.Infrastructure.Persistence;
using Modulith.Shared.Infrastructure.Time;
using Modulith.Shared.Kernel.Interfaces;
using OpenTelemetry;
using TickerQ.Utilities;
using TickerQ.Utilities.Entities;
using Wolverine;

namespace Modulith.Modules.Notifications;

public static class NotificationsModule
{
    public static IServiceCollection AddNotificationsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<NotificationsOptions>()
            .Bind(configuration.GetSection("Modules:Notifications"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<SmtpOptions>()
            .Bind(configuration.GetSection("Modules:Notifications:Smtp"))
            .ValidateOnStart();

        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddPermissions(NotificationsPermissions.All);

        services.AddSingleton<AuditableEntitySaveChangesInterceptor>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<INotificationStreamPublisher, InMemoryNotificationStreamPublisher>();
        services.AddScoped<NotificationRetentionPolicy>();

        services.AddDbContext<NotificationsDbContext>((sp, opts) =>
        {
            opts.UseNpgsql(
                configuration.GetConnectionString("db"),
                b => b.MigrationsHistoryTable("__ef_migrations_history", "notifications"));
            opts.AddInterceptors(sp.GetRequiredService<AuditableEntitySaveChangesInterceptor>());
        });

        services.AddScoped<IPersonalDataExporter, NotificationsPersonalDataExporter>();
        services.AddScoped<NotificationsPersonalDataEraser>();
        services.AddScoped<IPersonalDataEraser>(sp => sp.GetRequiredService<NotificationsPersonalDataEraser>());
        services.AddScoped<NotificationSendGuard>();

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
        opts.Discovery.IncludeType<OnUserErasureRequestedHandler>();
        opts.Discovery.IncludeType<CreateNotificationHandler>();
        opts.Discovery.IncludeType<ListMyNotificationsHandler>();
        opts.Discovery.IncludeType<GetUnreadNotificationCountHandler>();
        opts.Discovery.IncludeType<MarkNotificationAsReadHandler>();
        opts.Discovery.IncludeType<MarkAllNotificationsAsReadHandler>();
        opts.Discovery.IncludeType<ArchiveNotificationHandler>();
        opts.Discovery.IncludeType<GetMyNotificationPreferencesHandler>();
        opts.Discovery.IncludeType<UpdateMyNotificationPreferencesHandler>();
        opts.Discovery.IncludeType<StreamMyNotificationsHandler>();
        opts.Discovery.IncludeType<PruneNotificationsHandler>();
        return opts;
    }

    public static TickerOptionsBuilder<TimeTickerEntity, CronTickerEntity> AddNotificationsJobs(
        this TickerOptionsBuilder<TimeTickerEntity, CronTickerEntity> opts)
    {
        _ = typeof(PruneNotificationsJob);
        return opts;
    }

    public static IEndpointRouteBuilder MapNotificationsEndpoints(this IEndpointRouteBuilder app)
    {
        ListMyNotificationsEndpoint.Map(app);
        GetUnreadNotificationCountEndpoint.Map(app);
        MarkNotificationAsReadEndpoint.Map(app);
        MarkAllNotificationsAsReadEndpoint.Map(app);
        ArchiveNotificationEndpoint.Map(app);
        StreamMyNotificationsEndpoint.Map(app);
        GetMyNotificationPreferencesEndpoint.Map(app);
        UpdateMyNotificationPreferencesEndpoint.Map(app);
        return app;
    }
}
