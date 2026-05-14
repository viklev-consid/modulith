using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Modules.Audit.Authorization;
using Modulith.Modules.Audit.Contracts.Authorization;
using Modulith.Modules.Audit.Features.GetAuditTrail;
using Modulith.Modules.Audit.Gdpr;
using Modulith.Modules.Audit.Integration.Subscribers;
using Modulith.Modules.Audit.Persistence;
using Modulith.Shared.Infrastructure.Authorization;
using Modulith.Shared.Infrastructure.Persistence;
using Modulith.Shared.Kernel.Interfaces;
using OpenTelemetry;
using Wolverine;

namespace Modulith.Modules.Audit;

public static class AuditModule
{
    public static IServiceCollection AddAuditModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<AuditableEntitySaveChangesInterceptor>();
        services.AddPermissions(AuditPermissions.All);

        services.AddDbContext<AuditDbContext>((sp, opts) =>
        {
            opts.UseNpgsql(
                configuration.GetConnectionString("db"),
                b => b.MigrationsHistoryTable("__ef_migrations_history", "audit"));
            opts.AddInterceptors(sp.GetRequiredService<AuditableEntitySaveChangesInterceptor>());
        });

        services.AddScoped<IPersonalDataExporter, AuditPersonalDataExporter>();
        services.AddScoped<AuditPersonalDataEraser>();
        services.AddScoped<IPersonalDataEraser>(sp => sp.GetRequiredService<AuditPersonalDataEraser>());

        services.AddSingleton<IResourcePolicy<AuditTrailResource>, AuditTrailPolicy>();

        services.AddHealthChecks()
            .AddDbContextCheck<AuditDbContext>("audit-db", tags: ["ready"]);

        services.AddOpenTelemetry()
            .WithTracing(t => t.AddSource(AuditTelemetry.SourceName))
            .WithMetrics(m => m.AddMeter(AuditTelemetry.MeterName));

        return services;
    }

    public static WolverineOptions AddAuditHandlers(this WolverineOptions opts)
    {
        opts.Discovery.IncludeType<GetAuditTrailHandler>();
        opts.Discovery.IncludeType<OnUserRegisteredHandler>();
        opts.Discovery.IncludeType<OnUserEmailChangedHandler>();
        opts.Discovery.IncludeType<OnUserLoggedInHandler>();
        opts.Discovery.IncludeType<OnUserLoggedOutHandler>();
        opts.Discovery.IncludeType<OnUserLoggedOutAllDevicesHandler>();
        opts.Discovery.IncludeType<OnPasswordResetHandler>();
        opts.Discovery.IncludeType<OnPasswordChangedHandler>();
        opts.Discovery.IncludeType<OnEmailChangedHandler>();
        opts.Discovery.IncludeType<OnUserRoleChangedHandler>();

        // External login audit subscribers — Phase 14
        opts.Discovery.IncludeType<OnExternalLoginLinkedHandler>();
        opts.Discovery.IncludeType<OnExternalLoginUnlinkedHandler>();
        opts.Discovery.IncludeType<OnUserOnboardingCompletedHandler>();
        opts.Discovery.IncludeType<OnUserProvisionedFromExternalHandler>();
        opts.Discovery.IncludeType<OnUserErasureRequestedHandler>();
        opts.Discovery.IncludeType<OnRefreshTokenReuseDetectedHandler>();
        opts.Discovery.IncludeType<OnTwoFactorEnabledHandler>();
        opts.Discovery.IncludeType<OnTwoFactorDisabledHandler>();
        opts.Discovery.IncludeType<OnRecoveryCodesRegeneratedHandler>();
        return opts;
    }

    public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder app)
    {
        GetAuditTrailEndpoint.Map(app);
        return app;
    }
}
