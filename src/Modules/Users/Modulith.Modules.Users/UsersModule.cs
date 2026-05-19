using FluentValidation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Modulith.Modules.Users.Avatars;
using Modulith.Modules.Users.ConsentManagement;
using Modulith.Modules.Users.Contracts.Authorization;
using Modulith.Modules.Users.Features.ChangePassword;
using Modulith.Modules.Users.Features.ChangeUserRole;
using Modulith.Modules.Users.Features.CompleteOnboarding;
using Modulith.Modules.Users.Features.ConfirmEmail;
using Modulith.Modules.Users.Features.ConfirmEmailChange;
using Modulith.Modules.Users.Features.CreateInvitation;
using Modulith.Modules.Users.Features.DeleteAccount;
using Modulith.Modules.Users.Features.DeleteAvatar;
using Modulith.Modules.Users.Features.ExportPersonalData;
using Modulith.Modules.Users.Features.ForgotPassword;
using Modulith.Modules.Users.Features.GetCurrentUser;
using Modulith.Modules.Users.Features.GetUserAvatar;
using Modulith.Modules.Users.Features.GetUserById;
using Modulith.Modules.Users.Features.ListInvitations;
using Modulith.Modules.Users.Features.ListUsers;
using Modulith.Modules.Users.Features.Login;
using Modulith.Modules.Users.Features.LoginTwoFactor;
using Modulith.Modules.Users.Features.Logout;
using Modulith.Modules.Users.Features.LogoutAll;
using Modulith.Modules.Users.Features.RefreshToken;
using Modulith.Modules.Users.Features.Register;
using Modulith.Modules.Users.Features.RequestEmailChange;
using Modulith.Modules.Users.Features.ResendEmailConfirmation;
using Modulith.Modules.Users.Features.ResetPassword;
using Modulith.Modules.Users.Features.RevokeInvitation;
using Modulith.Modules.Users.Features.TwoFactor.ConfirmTotp;
using Modulith.Modules.Users.Features.TwoFactor.DisableTwoFactor;
using Modulith.Modules.Users.Features.TwoFactor.RegenerateRecoveryCodes;
using Modulith.Modules.Users.Features.TwoFactor.SetupTotp;
using Modulith.Modules.Users.Features.UpdateAvatar;
using Modulith.Modules.Users.Features.UpdateProfile;
using Modulith.Modules.Users.Gdpr;
using Modulith.Modules.Users.Jobs;
using Modulith.Modules.Users.Persistence;
using Modulith.Modules.Users.Security;
using Modulith.Modules.Users.Seeding;
using Modulith.Shared.Infrastructure.Authorization;
using Modulith.Shared.Infrastructure.Identity;
using Modulith.Shared.Infrastructure.Persistence;
using Modulith.Shared.Infrastructure.Seeding;
using Modulith.Shared.Infrastructure.Time;
using Modulith.Shared.Kernel.Interfaces;
using OpenTelemetry;
using TickerQ.Utilities;
using TickerQ.Utilities.Entities;
using Wolverine;
using Wolverine.EntityFrameworkCore;

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
            .Validate(o => Enum.IsDefined(o.Registration.Mode), "Registration mode must be a valid value.")
            .Validate(o => o.Registration.InvitationTokenLifetime > TimeSpan.Zero, "Invitation token lifetime must be greater than zero.")
            .ValidateOnStart();

        services.AddOptions<AdminBootstrapOptions>()
            .Bind(configuration.GetSection("Modules:Users:AdminBootstrap"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHttpContextAccessor();
        services.AddPermissions(UsersPermissions.All);
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<AuditableEntitySaveChangesInterceptor>();

        services.AddDbContextWithWolverineIntegration<UsersDbContext>((sp, opts) =>
        {
            opts.UseNpgsql(
                configuration.GetConnectionString("db"),
                b => b.MigrationsHistoryTable("__ef_migrations_history", "users"));
            opts.AddInterceptors(sp.GetRequiredService<AuditableEntitySaveChangesInterceptor>());
        });

        services.AddMemoryCache();

        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<IJwtGenerator, JwtGenerator>();
        services.AddScoped<IRefreshTokenIssuer, RefreshTokenIssuer>();
        services.AddScoped<IRefreshTokenRevoker, RefreshTokenRevoker>();
        services.AddScoped<ISingleUseTokenService, SingleUseTokenService>();
        services.AddScoped<ITotpService, TotpService>();
        services.AddScoped<ITotpSecretProtector, DataProtectionTotpSecretProtector>();
        services.AddScoped<ITwoFactorRequirementEvaluator, TwoFactorRequirementEvaluator>();
        services.AddScoped<ITwoFactorChallengeIssuer, TwoFactorChallengeIssuer>();
        services.AddScoped<IAvatarImageInspector, MagickAvatarImageInspector>();
        services.AddScoped<IUserAvatarStorage, UserAvatarStorage>();

        services.AddScoped<IConsentRegistry, UsersConsentRegistry>();
        services.AddScoped<IPersonalDataExporter, UsersPersonalDataExporter>();
        services.AddScoped<UsersPersonalDataEraser>();
        services.AddScoped<IPersonalDataEraser>(sp => sp.GetRequiredService<UsersPersonalDataEraser>());
        services.AddScoped<PersonalDataOrchestrator>();

        services.AddHealthChecks()
            .AddDbContextCheck<UsersDbContext>("users-db", tags: ["ready"]);

        services.AddOpenTelemetry()
            .WithTracing(t => t.AddSource(UsersTelemetry.SourceName))
            .WithMetrics(m => m.AddMeter(UsersTelemetry.MeterName));

        services.AddValidatorsFromAssemblyContaining<RegisterValidator>(ServiceLifetime.Scoped, includeInternalTypes: true);

        if (environment.IsDevelopment())
        {
            services.AddOptions<UsersDevOptions>()
                .Bind(configuration.GetSection("Modules:Users:Dev"))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddScoped<IModuleSeeder, UsersDevSeeder>();
        }
        else
        {
            services.AddHostedService<AdminBootstrapper>();
        }

        return services;
    }

    public static WolverineOptions AddUsersHandlers(this WolverineOptions opts)
    {
        opts.Discovery.IncludeType<RegisterHandler>();
        opts.Discovery.IncludeType<LoginHandler>();
        opts.Discovery.IncludeType<LoginTwoFactorHandler>();
        opts.Discovery.IncludeType<GetCurrentUserHandler>();
        opts.Discovery.IncludeType<CompleteOnboardingHandler>();
        opts.Discovery.IncludeType<UpdateProfileHandler>();
        opts.Discovery.IncludeType<UpdateAvatarHandler>();
        opts.Discovery.IncludeType<DeleteAvatarHandler>();
        opts.Discovery.IncludeType<GetUserAvatarHandler>();
        opts.Discovery.IncludeType<ExportPersonalDataHandler>();
        opts.Discovery.IncludeType<DeleteAccountHandler>();

        // Auth flow handlers — Phase 9.5
        opts.Discovery.IncludeType<ForgotPasswordHandler>();
        opts.Discovery.IncludeType<ResetPasswordHandler>();
        opts.Discovery.IncludeType<ChangePasswordHandler>();
        opts.Discovery.IncludeType<ConfirmEmailHandler>();
        opts.Discovery.IncludeType<RequestEmailChangeHandler>();
        opts.Discovery.IncludeType<ConfirmEmailChangeHandler>();
        opts.Discovery.IncludeType<ResendEmailConfirmationHandler>();
        opts.Discovery.IncludeType<RefreshTokenHandler>();
        opts.Discovery.IncludeType<LogoutHandler>();
        opts.Discovery.IncludeType<LogoutAllHandler>();
        opts.Discovery.IncludeType<SweepExpiredTokensHandler>();

        // RBAC management handlers — Phase 13
        opts.Discovery.IncludeType<ChangeUserRoleHandler>();
        opts.Discovery.IncludeType<ListUsersHandler>();
        opts.Discovery.IncludeType<GetUserByIdHandler>();
        opts.Discovery.IncludeType<ListInvitationsHandler>();
        opts.Discovery.IncludeType<CreateInvitationHandler>();
        opts.Discovery.IncludeType<RevokeInvitationHandler>();

        // Two-factor authentication
        opts.Discovery.IncludeType<SetupTotpHandler>();
        opts.Discovery.IncludeType<ConfirmTotpHandler>();
        opts.Discovery.IncludeType<DisableTwoFactorHandler>();
        opts.Discovery.IncludeType<RegenerateRecoveryCodesHandler>();

        return opts;
    }

    public static TickerOptionsBuilder<TimeTickerEntity, CronTickerEntity> AddUsersJobs(
        this TickerOptionsBuilder<TimeTickerEntity, CronTickerEntity> opts)
    {
        // TickerQ discovers cron jobs from [TickerFunction] attributes. Keep this
        // extension as the module-owned registration point for future job options.
        _ = typeof(SweepExpiredTokensJob);
        return opts;
    }

    public static IEndpointRouteBuilder MapUsersEndpoints(this IEndpointRouteBuilder app)
    {
        RegisterEndpoint.Map(app);
        LoginEndpoint.Map(app);
        LoginTwoFactorEndpoint.Map(app);
        GetCurrentUserEndpoint.Map(app);
        CompleteOnboardingEndpoint.Map(app);
        UpdateProfileEndpoint.Map(app);
        UpdateAvatarEndpoint.Map(app);
        DeleteAvatarEndpoint.Map(app);
        GetUserAvatarEndpoint.Map(app);
        ExportPersonalDataEndpoint.Map(app);
        DeleteAccountEndpoint.Map(app);

        // Auth flow endpoints — Phase 9.5
        ForgotPasswordEndpoint.Map(app);
        ResetPasswordEndpoint.Map(app);
        ChangePasswordEndpoint.Map(app);
        ConfirmEmailEndpoint.Map(app);
        RequestEmailChangeEndpoint.Map(app);
        ConfirmEmailChangeEndpoint.Map(app);
        ResendEmailConfirmationEndpoint.Map(app);
        RefreshTokenEndpoint.Map(app);
        LogoutEndpoint.Map(app);
        LogoutAllEndpoint.Map(app);

        // RBAC management endpoints — Phase 13
        ChangeUserRoleEndpoint.Map(app);
        ListUsersEndpoint.Map(app);
        GetUserByIdEndpoint.Map(app);
        ListInvitationsEndpoint.Map(app);
        CreateInvitationEndpoint.Map(app);
        RevokeInvitationEndpoint.Map(app);

        // Two-factor authentication
        SetupTotpEndpoint.Map(app);
        ConfirmTotpEndpoint.Map(app);
        DisableTwoFactorEndpoint.Map(app);
        RegenerateRecoveryCodesEndpoint.Map(app);

        return app;
    }
}
