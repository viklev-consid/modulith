using FluentValidation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Modulith.Modules.Catalog.Features.CreateProduct;
using Modulith.Modules.Catalog.Features.GetProductById;
using Modulith.Modules.Catalog.Features.ListProducts;
using Modulith.Modules.Catalog.Integration.Subscribers;
using Modulith.Modules.Catalog.Persistence;
using Modulith.Modules.Catalog.Seeding;
using Modulith.Shared.Infrastructure.Persistence;
using Modulith.Shared.Infrastructure.Seeding;
using Wolverine;

namespace Modulith.Modules.Catalog;

public static class CatalogModule
{
    public static IServiceCollection AddCatalogModule(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        services.AddOptions<CatalogOptions>()
            .Bind(configuration.GetSection("Modules:Catalog"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddScoped<AuditableEntitySaveChangesInterceptor>();

        services.AddDbContext<CatalogDbContext>((sp, opts) =>
        {
            opts.UseNpgsql(
                configuration.GetConnectionString("db"),
                b => b.MigrationsHistoryTable("__ef_migrations_history", "catalog"));
            opts.AddInterceptors(sp.GetRequiredService<AuditableEntitySaveChangesInterceptor>());
        });

        services.AddValidatorsFromAssemblyContaining<CreateProductValidator>(ServiceLifetime.Scoped, includeInternalTypes: true);

        if (environment.IsDevelopment())
            services.AddScoped<IModuleSeeder, CatalogDevSeeder>();

        return services;
    }

    public static WolverineOptions AddCatalogHandlers(this WolverineOptions opts)
    {
        opts.Discovery.IncludeType<CreateProductHandler>();
        opts.Discovery.IncludeType<GetProductByIdHandler>();
        opts.Discovery.IncludeType<ListProductsHandler>();
        opts.Discovery.IncludeType<OnUserRegisteredHandler>();
        return opts;
    }

    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        ListProductsEndpoint.Map(app);
        CreateProductEndpoint.Map(app);
        GetProductByIdEndpoint.Map(app);
        return app;
    }
}
