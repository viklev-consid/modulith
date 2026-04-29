using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DotNet.Testcontainers.Builders;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Respawn;
using Respawn.Graph;
using Testcontainers.PostgreSql;
using Xunit;

namespace Modulith.TestSupport;

/// <summary>
/// Base fixture for module integration tests. Starts a PostgreSQL container,
/// runs migrations once per class, and resets data between tests via Respawn.
/// </summary>
public abstract class ApiTestFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string TestJwtKey = "test-jwt-key-for-integration-tests-at-least-32chars";
    public const string TestJwtIssuer = "modulith-test";
    public const string TestJwtAudience = "modulith-test";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .Build();

    private Respawner? _respawner;
    public string ConnectionString => _postgres.GetConnectionString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.UseSetting("ConnectionStrings:db", ConnectionString);
        builder.UseSetting("Jwt:Issuer", TestJwtIssuer);
        builder.UseSetting("Jwt:Audience", TestJwtAudience);
        builder.UseSetting("Jwt:SigningKey", TestJwtKey);

        // Satisfies GoogleAuthOptions [Required] validation so all tests can start.
        builder.UseSetting("Modules:Users:Google:ClientId", "test-google-client-id");

        builder.ConfigureServices(services =>
        {
            // Cap host shutdown so Wolverine's DurabilityAgent (which retries against the
            // already-stopped Postgres container) cannot keep the test process alive past
            // this deadline. Default is 30 s; 5 s is ample for any real graceful-stop work.
            services.Configure<HostOptions>(opts => opts.ShutdownTimeout = TimeSpan.FromSeconds(5));
            ConfigureTestServices(services);
        });
    }

    protected virtual void ConfigureTestServices(IServiceCollection services) { }

    /// <summary>
    /// The generic <see cref="IHost"/> backing this factory. Available after
    /// <see cref="IAsyncLifetime.InitializeAsync"/> has run.
    /// Use this to call Wolverine's <c>TrackActivity()</c> in integration tests.
    /// </summary>
    public IHost ApplicationHost { get; private set; } = null!;

    protected override IHost CreateHost(IHostBuilder builder)
    {
        ApplicationHost = base.CreateHost(builder);
        return ApplicationHost;
    }

    async Task IAsyncLifetime.InitializeAsync()
    {
        await _postgres.StartAsync();

        // Allow subclasses to start additional containers (e.g. Mailpit) before the host
        // is built, so their ports are available inside ConfigureWebHost.
        await StartAdditionalContainersAsync();

        // Trigger host build (reads ConnectionString set above) then migrate.
        // Wolverine auto-provisions its schema when its hosted service starts.
        using var scope = Services.CreateScope();
        await MigrateAsync(scope.ServiceProvider);

        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();

        _respawner = await Respawner.CreateAsync(conn, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = [.. GetSchemasToReset(), "wolverine"],
            TablesToIgnore =
            [
                new Table("wolverine", "wolverine_nodes"),
                new Table("wolverine", "wolverine_node_assignments"),
            ],
        });
    }

    /// <summary>
    /// Override to start extra containers (e.g. Mailpit) before the WebApplicationFactory
    /// host is built. Ports are knowable here and usable in ConfigureWebHost overrides.
    /// </summary>
    protected virtual Task StartAdditionalContainersAsync() => Task.CompletedTask;

    protected abstract Task MigrateAsync(IServiceProvider services);

    protected virtual string[] GetSchemasToReset() => [];

    public async Task ResetDatabaseAsync()
    {
        if (_respawner is null)
        {
            return;
        }

        // Retry on PostgreSQL deadlock (40P01): the Wolverine DurabilityAgent may hold
        // short-lived locks on the wolverine schema concurrently with Respawn's DELETE sweep.
        // A brief wait lets the in-flight agent transaction complete.
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            await using var conn = new NpgsqlConnection(ConnectionString);
            await conn.OpenAsync();
            try
            {
                await _respawner.ResetAsync(conn);
                return;
            }
            catch (Npgsql.PostgresException ex) when (string.Equals(ex.SqlState, "40P01", StringComparison.Ordinal) && attempt < 3)
            {
                await Task.Delay(150 * attempt);
            }
        }
    }

    public HttpClient CreateAnonymousClient() => CreateClient();

    public HttpClient CreateAuthenticatedClient(Guid userId, string email, string displayName, string role = "user")
    {
        var token = GenerateTestToken(userId, email, displayName, role);
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>Creates an authenticated client using an existing JWT (e.g. from a real login response).</summary>
    public HttpClient CreateAuthenticatedClientWithToken(string accessToken)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    public static string GenerateTestToken(Guid userId, string email, string displayName, string role = "user")
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestJwtKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(ClaimTypes.Name, displayName),
            new Claim(ClaimTypes.Role, role),
        };

        var token = new JwtSecurityToken(
            issuer: TestJwtIssuer,
            audience: TestJwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        // Stop the host with a hard deadline before tearing down the database.
        // Wolverine's DurabilityAgent polls Postgres in a background loop; without
        // a deadline it keeps retrying after the container stops, blocking process exit.
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await ApplicationHost.StopAsync(cts.Token);
        }
        catch (OperationCanceledException) { /* deadline reached — expected */ }

        await _postgres.DisposeAsync();
        await DisposeAdditionalContainersAsync();

        // Explicitly dispose WebApplicationFactory (TestServer, service provider)
        // since xUnit only calls IAsyncLifetime.DisposeAsync(), not base Dispose().
        // WebApplicationFactory only implements IDisposable (not IAsyncDisposable).
#pragma warning disable S6966
        Dispose();
#pragma warning restore S6966
    }

    /// <summary>Override to dispose extra containers started in StartAdditionalContainersAsync.</summary>
    protected virtual Task DisposeAdditionalContainersAsync() => Task.CompletedTask;
}
