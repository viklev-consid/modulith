using FluentValidation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Modules.ModuleName.Persistence;
using Modulith.Shared.Infrastructure.Persistence;
using Wolverine;

namespace Modulith.Modules.ModuleName;

public static class ModuleNameModule
{
    public static IServiceCollection AddModuleNameModule(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        services.AddScoped<AuditableEntitySaveChangesInterceptor>();

        services.AddDbContext<ModuleNameDbContext>((sp, opts) =>
        {
            opts.UseNpgsql(
                configuration.GetConnectionString("db"),
                b => b.MigrationsHistoryTable("__ef_migrations_history", "modulenamelower"));
            opts.AddInterceptors(sp.GetRequiredService<AuditableEntitySaveChangesInterceptor>());
        });

        services.AddValidatorsFromAssemblyContaining<ModuleNameDbContext>(
            ServiceLifetime.Scoped, includeInternalTypes: true);

        // TODO: register IPersonalDataExporter / IPersonalDataEraser if this module holds personal data

        return services;
    }

    public static WolverineOptions AddModuleNameHandlers(this WolverineOptions opts)
    {
        // TODO: opts.Discovery.IncludeType<YourHandler>();
        return opts;
    }

    public static IEndpointRouteBuilder MapModuleNameEndpoints(this IEndpointRouteBuilder app)
    {
        // TODO: YourEndpoint.Map(app);
        return app;
    }
}
