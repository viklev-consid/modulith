using FluentValidation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Modulith.Modules.Organizations.Authorization;
using Modulith.Modules.Organizations.Contracts.Authorization;
using Modulith.Modules.Organizations.Features.AcceptOrganizationInvitation;
using Modulith.Modules.Organizations.Features.ChangeOrganizationMemberRole;
using Modulith.Modules.Organizations.Features.CreateOrganization;
using Modulith.Modules.Organizations.Features.CreateOrganizationInvitation;
using Modulith.Modules.Organizations.Features.DeleteOrganization;
using Modulith.Modules.Organizations.Features.EnsureUserCanBeErasedFromOrganizations;
using Modulith.Modules.Organizations.Features.GetOrganization;
using Modulith.Modules.Organizations.Features.GetOrganizationAudit;
using Modulith.Modules.Organizations.Features.ListMyOrganizations;
using Modulith.Modules.Organizations.Features.ListOrganizationInvitations;
using Modulith.Modules.Organizations.Features.ListOrganizationMembers;
using Modulith.Modules.Organizations.Features.RemoveOrganizationMember;
using Modulith.Modules.Organizations.Features.RevokeOrganizationInvitation;
using Modulith.Modules.Organizations.Features.UpdateOrganization;
using Modulith.Modules.Organizations.Gdpr;
using Modulith.Modules.Organizations.Integration.Subscribers;
using Modulith.Modules.Organizations.Persistence;
using Modulith.Modules.Organizations.Seeding;
using Modulith.Shared.Infrastructure.Authorization;
using Modulith.Shared.Infrastructure.Persistence;
using Modulith.Shared.Infrastructure.Seeding;
using Modulith.Shared.Infrastructure.Time;
using Modulith.Shared.Kernel.Interfaces;
using OpenTelemetry;
using Wolverine;
using Wolverine.EntityFrameworkCore;

namespace Modulith.Modules.Organizations;

public static class OrganizationsModule
{
    public static IServiceCollection AddOrganizationsModule(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        services.TryAddSingleton<IClock, SystemClock>();
        services.AddScoped<AuditableEntitySaveChangesInterceptor>();
        services.AddPermissions(OrganizationsPermissions.All);
        services.AddScoped<IOrganizationRefResolver, OrganizationRefResolver>();
        services.AddScoped<IScopedAuthorizationService<OrganizationScope>, OrganizationScopedAuthorizationService>();

        services.AddDbContextWithWolverineIntegration<OrganizationsDbContext>((sp, opts) =>
        {
            opts.UseNpgsql(
                configuration.GetConnectionString("db"),
                b => b.MigrationsHistoryTable("__ef_migrations_history", "organizations"));
            opts.AddInterceptors(sp.GetRequiredService<AuditableEntitySaveChangesInterceptor>());
        });

        services.AddValidatorsFromAssemblyContaining<OrganizationsDbContext>(
            ServiceLifetime.Scoped, includeInternalTypes: true);

        services.AddScoped<IPersonalDataExporter, OrganizationsPersonalDataExporter>();
        services.AddScoped<OrganizationsPersonalDataEraser>();
        services.AddScoped<IPersonalDataEraser>(sp => sp.GetRequiredService<OrganizationsPersonalDataEraser>());

        services.AddHealthChecks()
            .AddDbContextCheck<OrganizationsDbContext>("organizations-db", tags: ["ready"]);

        services.AddOpenTelemetry()
            .WithTracing(t => t.AddSource(OrganizationsTelemetry.SourceName))
            .WithMetrics(m => m.AddMeter(OrganizationsTelemetry.MeterName));

        if (environment.IsDevelopment())
        {
            services.AddScoped<IModuleSeeder, OrganizationsDevSeeder>();
        }

        return services;
    }

    public static WolverineOptions AddOrganizationsHandlers(this WolverineOptions opts)
    {
        opts.Discovery.IncludeType<CreateOrganizationHandler>();
        opts.Discovery.IncludeType<ListMyOrganizationsHandler>();
        opts.Discovery.IncludeType<GetOrganizationHandler>();
        opts.Discovery.IncludeType<UpdateOrganizationHandler>();
        opts.Discovery.IncludeType<DeleteOrganizationHandler>();
        opts.Discovery.IncludeType<ListOrganizationMembersHandler>();
        opts.Discovery.IncludeType<ChangeOrganizationMemberRoleHandler>();
        opts.Discovery.IncludeType<RemoveOrganizationMemberHandler>();
        opts.Discovery.IncludeType<CreateOrganizationInvitationHandler>();
        opts.Discovery.IncludeType<AcceptOrganizationInvitationHandler>();
        opts.Discovery.IncludeType<ListOrganizationInvitationsHandler>();
        opts.Discovery.IncludeType<RevokeOrganizationInvitationHandler>();
        opts.Discovery.IncludeType<EnsureUserCanBeErasedFromOrganizationsHandler>();
        opts.Discovery.IncludeType<OnUserErasureRequestedHandler>();
        return opts;
    }

    public static IEndpointRouteBuilder MapOrganizationsEndpoints(this IEndpointRouteBuilder app)
    {
        CreateOrganizationEndpoint.Map(app);
        ListMyOrganizationsEndpoint.Map(app);
        GetOrganizationEndpoint.Map(app);
        UpdateOrganizationEndpoint.Map(app);
        DeleteOrganizationEndpoint.Map(app);
        ListOrganizationMembersEndpoint.Map(app);
        ChangeOrganizationMemberRoleEndpoint.Map(app);
        RemoveOrganizationMemberEndpoint.Map(app);
        CreateOrganizationInvitationEndpoint.Map(app);
        AcceptOrganizationInvitationEndpoint.Map(app);
        ListOrganizationInvitationsEndpoint.Map(app);
        RevokeOrganizationInvitationEndpoint.Map(app);
        GetOrganizationAuditEndpoint.Map(app);
        return app;
    }
}
