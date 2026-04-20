using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Modules.Audit.Features.GetAuditTrail;
using Modulith.Modules.Audit.Gdpr;
using Modulith.Modules.Audit.Integration.Subscribers;
using Modulith.Modules.Audit.Persistence;
using Modulith.Shared.Infrastructure.Persistence;
using Modulith.Shared.Kernel.Interfaces;
using Wolverine;

namespace Modulith.Modules.Audit;

public static class AuditModule
{
    public static IServiceCollection AddAuditModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<AuditableEntitySaveChangesInterceptor>();

        services.AddDbContext<AuditDbContext>((sp, opts) =>
        {
            opts.UseNpgsql(
                configuration.GetConnectionString("db"),
                b => b.MigrationsHistoryTable("__ef_migrations_history", "audit"));
            opts.AddInterceptors(sp.GetRequiredService<AuditableEntitySaveChangesInterceptor>());
        });

        services.AddScoped<IPersonalDataExporter, AuditPersonalDataExporter>();
        services.AddScoped<IPersonalDataEraser, AuditPersonalDataEraser>();

        return services;
    }

    public static WolverineOptions AddAuditHandlers(this WolverineOptions opts)
    {
        opts.Discovery.IncludeType<GetAuditTrailHandler>();
        opts.Discovery.IncludeType<OnUserRegisteredHandler>();
        opts.Discovery.IncludeType<OnUserEmailChangedHandler>();
        return opts;
    }

    public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder app)
    {
        GetAuditTrailEndpoint.Map(app);
        return app;
    }
}
