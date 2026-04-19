using FluentValidation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Modulith.Modules.Users.Features.GetCurrentUser;
using Modulith.Modules.Users.Features.Login;
using Modulith.Modules.Users.Features.Register;
using Modulith.Modules.Users.Persistence;
using Modulith.Modules.Users.Security;
using Modulith.Modules.Users.Seeding;
using Modulith.Shared.Infrastructure.Identity;
using Modulith.Shared.Infrastructure.Persistence;
using Modulith.Shared.Infrastructure.Seeding;
using Modulith.Shared.Infrastructure.Time;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Users;

public static class UsersModule
{
    public static IServiceCollection AddUsersModule(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        services.AddOptions<UsersOptions>()
            .Bind(configuration.GetSection("Modules:Users"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<AuditableEntitySaveChangesInterceptor>();

        services.AddDbContext<UsersDbContext>((sp, opts) =>
        {
            opts.UseNpgsql(
                configuration.GetConnectionString("db"),
                b => b.MigrationsHistoryTable("__ef_migrations_history", "users"));
            opts.AddInterceptors(sp.GetRequiredService<AuditableEntitySaveChangesInterceptor>());
        });

        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<IJwtGenerator, JwtGenerator>();

        services.AddValidatorsFromAssemblyContaining<RegisterValidator>(ServiceLifetime.Scoped);

        if (environment.IsDevelopment())
            services.AddScoped<IModuleSeeder, UsersDevSeeder>();

        return services;
    }

    public static IEndpointRouteBuilder MapUsersEndpoints(this IEndpointRouteBuilder app)
    {
        RegisterEndpoint.Map(app);
        LoginEndpoint.Map(app);
        GetCurrentUserEndpoint.Map(app);
        return app;
    }
}
