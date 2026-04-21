using FluentValidation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Modulith.Modules.Users.ConsentManagement;
using Modulith.Modules.Users.Features.ChangePassword;
using Modulith.Modules.Users.Features.ConfirmEmailChange;
using Modulith.Modules.Users.Features.DeleteAccount;
using Modulith.Modules.Users.Features.ExportPersonalData;
using Modulith.Modules.Users.Features.ForgotPassword;
using Modulith.Modules.Users.Features.GetCurrentUser;
using Modulith.Modules.Users.Features.Login;
using Modulith.Modules.Users.Features.Logout;
using Modulith.Modules.Users.Features.LogoutAll;
using Modulith.Modules.Users.Features.RefreshToken;
using Modulith.Modules.Users.Features.Register;
using Modulith.Modules.Users.Features.RequestEmailChange;
using Modulith.Modules.Users.Features.ResetPassword;
using Modulith.Modules.Users.Gdpr;
using Modulith.Modules.Users.Jobs;
using Modulith.Modules.Users.Persistence;
using Modulith.Modules.Users.Security;
using Modulith.Modules.Users.Seeding;
using Modulith.Shared.Infrastructure.Identity;
using Modulith.Shared.Infrastructure.Persistence;
using Modulith.Shared.Infrastructure.Seeding;
using Modulith.Shared.Infrastructure.Time;
using Modulith.Shared.Kernel.Interfaces;
using Wolverine;

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
        services.AddScoped<IRefreshTokenIssuer, RefreshTokenIssuer>();
        services.AddScoped<ISingleUseTokenService, SingleUseTokenService>();

        services.AddScoped<IConsentRegistry, UsersConsentRegistry>();
        services.AddScoped<IPersonalDataExporter, UsersPersonalDataExporter>();
        services.AddScoped<IPersonalDataEraser, UsersPersonalDataEraser>();
        services.AddScoped<PersonalDataOrchestrator>();

        services.AddValidatorsFromAssemblyContaining<RegisterValidator>(ServiceLifetime.Scoped, includeInternalTypes: true);

        if (environment.IsDevelopment())
            services.AddScoped<IModuleSeeder, UsersDevSeeder>();

        return services;
    }

    public static WolverineOptions AddUsersHandlers(this WolverineOptions opts)
    {
        opts.Discovery.IncludeType<RegisterHandler>();
        opts.Discovery.IncludeType<LoginHandler>();
        opts.Discovery.IncludeType<GetCurrentUserHandler>();
        opts.Discovery.IncludeType<ExportPersonalDataHandler>();
        opts.Discovery.IncludeType<DeleteAccountHandler>();

        // Auth flow handlers — Phase 9.5
        opts.Discovery.IncludeType<ForgotPasswordHandler>();
        opts.Discovery.IncludeType<ResetPasswordHandler>();
        opts.Discovery.IncludeType<ChangePasswordHandler>();
        opts.Discovery.IncludeType<RequestEmailChangeHandler>();
        opts.Discovery.IncludeType<ConfirmEmailChangeHandler>();
        opts.Discovery.IncludeType<RefreshTokenHandler>();
        opts.Discovery.IncludeType<LogoutHandler>();
        opts.Discovery.IncludeType<LogoutAllHandler>();
        opts.Discovery.IncludeType<SweepExpiredTokensHandler>();

        return opts;
    }

    public static IEndpointRouteBuilder MapUsersEndpoints(this IEndpointRouteBuilder app)
    {
        RegisterEndpoint.Map(app);
        LoginEndpoint.Map(app);
        GetCurrentUserEndpoint.Map(app);
        ExportPersonalDataEndpoint.Map(app);
        DeleteAccountEndpoint.Map(app);

        // Auth flow endpoints — Phase 9.5
        ForgotPasswordEndpoint.Map(app);
        ResetPasswordEndpoint.Map(app);
        ChangePasswordEndpoint.Map(app);
        RequestEmailChangeEndpoint.Map(app);
        ConfirmEmailChangeEndpoint.Map(app);
        RefreshTokenEndpoint.Map(app);
        LogoutEndpoint.Map(app);
        LogoutAllEndpoint.Map(app);

        return app;
    }
}
