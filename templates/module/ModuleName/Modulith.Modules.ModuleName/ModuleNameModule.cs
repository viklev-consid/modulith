using FluentValidation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Modulith.Modules.ModuleName.Contracts.Authorization;
using Modulith.Modules.ModuleName.Gdpr;
using Modulith.Modules.ModuleName.Persistence;
using Modulith.Modules.ModuleName.Seeding;
using Modulith.Shared.Infrastructure.Authorization;
using Modulith.Shared.Infrastructure.Persistence;
using Modulith.Shared.Infrastructure.Seeding;
using Modulith.Shared.Kernel.Interfaces;
using OpenTelemetry;
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
        services.AddPermissions(ModuleNamePermissions.All);

        services.AddDbContext<ModuleNameDbContext>((sp, opts) =>
        {
            opts.UseNpgsql(
                configuration.GetConnectionString("db"),
                b => b.MigrationsHistoryTable("__ef_migrations_history", "modulenamelower"));
            opts.AddInterceptors(sp.GetRequiredService<AuditableEntitySaveChangesInterceptor>());
        });

        services.AddValidatorsFromAssemblyContaining<ModuleNameDbContext>(
            ServiceLifetime.Scoped, includeInternalTypes: true);

        services.AddScoped<IPersonalDataExporter, ModuleNamePersonalDataExporter>();
        services.AddScoped<ModuleNamePersonalDataEraser>();
        services.AddScoped<IPersonalDataEraser>(sp => sp.GetRequiredService<ModuleNamePersonalDataEraser>());

        services.AddHealthChecks()
            .AddDbContextCheck<ModuleNameDbContext>("modulenamelower-db", tags: ["ready"]);

        services.AddOpenTelemetry()
            .WithTracing(t => t.AddSource(ModuleNameTelemetry.SourceName))
            .WithMetrics(m => m.AddMeter(ModuleNameTelemetry.MeterName));

        if (environment.IsDevelopment())
        {
            services.AddScoped<IModuleSeeder, ModuleNameDevSeeder>();
        }

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
