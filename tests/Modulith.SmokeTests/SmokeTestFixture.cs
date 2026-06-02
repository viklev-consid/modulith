using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Modules.Audit.Persistence;
using Modulith.Modules.Catalog.Persistence;
using Modulith.Modules.Notifications.Persistence;
using Modulith.Modules.Organizations.Persistence;
using Modulith.Modules.Users.Persistence;
using Modulith.TestSupport;

namespace Modulith.SmokeTests;

[CollectionDefinition("Smoke")]
public sealed class SmokeCollection : ICollectionFixture<SmokeTestFixture> { }

/// <summary>
/// Smoke-test fixture. Starts Postgres + a Mailpit container, migrates all module schemas,
/// and configures the real SmtpEmailSender to point at Mailpit's SMTP port.
/// </summary>
public sealed class SmokeTestFixture : ApiTestFixture
{
    // Mailpit: SMTP on 1025, HTTP API on 8025.
    private readonly IContainer mailpit = new ContainerBuilder("axllent/mailpit:v1.30.0")
        .WithPortBinding(1025, assignRandomHostPort: true)
        .WithPortBinding(8025, assignRandomHostPort: true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(8025, _ => { }))
        .Build();

    /// <summary>Base URL of the Mailpit HTTP API (e.g. http://localhost:PORT).</summary>
    public string MailpitApiUrl =>
        $"http://localhost:{mailpit.GetMappedPublicPort(8025)}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Use Development so the OpenAPI endpoint is publicly reachable (no RequireAuthorization).
        builder.UseEnvironment("Development");

        builder.UseSetting("ConnectionStrings:db", ConnectionString);
        builder.UseSetting("Jwt:Issuer", TestJwtIssuer);
        builder.UseSetting("Jwt:Audience", TestJwtAudience);
        builder.UseSetting("Jwt:SigningKey", TestJwtKey);
        builder.UseSetting("Modules:Seeders:Enabled", "false");

        // Point the real SmtpEmailSender at the Mailpit container.
        builder.UseSetting("Modules:Notifications:Smtp:Host", "127.0.0.1");
        builder.UseSetting("Modules:Notifications:Smtp:Port",
            mailpit.GetMappedPublicPort(1025).ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    protected override async Task StartAdditionalContainersAsync()
    {
        await mailpit.StartAsync();
    }

    protected override async Task DisposeAdditionalContainersAsync()
    {
        await mailpit.DisposeAsync();
    }

    protected override async Task MigrateAsync(IServiceProvider services)
    {
        await services.GetRequiredService<UsersDbContext>().Database.MigrateAsync();
        await services.GetRequiredService<CatalogDbContext>().Database.MigrateAsync();
        await services.GetRequiredService<AuditDbContext>().Database.MigrateAsync();
        await services.GetRequiredService<NotificationsDbContext>().Database.MigrateAsync();
        await services.GetRequiredService<OrganizationsDbContext>().Database.MigrateAsync();
    }

    protected override string[] GetSchemasToReset() =>
        ["users", "catalog", "audit", "notifications", "organizations"];
}
