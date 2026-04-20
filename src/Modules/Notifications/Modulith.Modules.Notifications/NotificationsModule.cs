using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Modules.Notifications.Consent;
using Modulith.Modules.Notifications.Integration.Subscribers;
using Modulith.Modules.Notifications.Persistence;
using Modulith.Shared.Infrastructure.Notifications;
using Modulith.Shared.Infrastructure.Persistence;
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
        services.AddScoped<IConsentRegistry, AlwaysGrantedConsentRegistry>();

        services.AddScoped<AuditableEntitySaveChangesInterceptor>();

        services.AddDbContext<NotificationsDbContext>((sp, opts) =>
        {
            opts.UseNpgsql(
                configuration.GetConnectionString("db"),
                b => b.MigrationsHistoryTable("__ef_migrations_history", "notifications"));
            opts.AddInterceptors(sp.GetRequiredService<AuditableEntitySaveChangesInterceptor>());
        });

        return services;
    }

    public static WolverineOptions AddNotificationsHandlers(this WolverineOptions opts)
    {
        opts.Discovery.IncludeType<OnUserRegisteredHandler>();
        return opts;
    }

    public static IEndpointRouteBuilder MapNotificationsEndpoints(this IEndpointRouteBuilder app)
    {
        // No public endpoints for Notifications in Phase 7.
        return app;
    }
}
