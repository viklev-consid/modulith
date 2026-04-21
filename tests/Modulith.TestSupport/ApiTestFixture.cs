using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DotNet.Testcontainers.Builders;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Respawn;
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

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
        .Build();

    private Respawner? _respawner;
    protected string ConnectionString => _postgres.GetConnectionString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.UseSetting("ConnectionStrings:db", ConnectionString);
        builder.UseSetting("Jwt:Issuer", TestJwtIssuer);
        builder.UseSetting("Jwt:Audience", TestJwtAudience);
        builder.UseSetting("Jwt:SigningKey", TestJwtKey);

        builder.ConfigureServices(services => ConfigureTestServices(services));
    }

    protected virtual void ConfigureTestServices(IServiceCollection services) { }

    async Task IAsyncLifetime.InitializeAsync()
    {
        await _postgres.StartAsync();

        // Trigger host build (reads ConnectionString set above) then migrate.
        using var scope = Services.CreateScope();
        await MigrateAsync(scope.ServiceProvider);

        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();

        _respawner = await Respawner.CreateAsync(conn, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = GetSchemasToReset(),
        });
    }

    protected abstract Task MigrateAsync(IServiceProvider services);

    protected virtual string[] GetSchemasToReset() => [];

    public async Task ResetDatabaseAsync()
    {
        if (_respawner is null) return;
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        await _respawner.ResetAsync(conn);
    }

    public HttpClient CreateAnonymousClient() => CreateClient();

    public HttpClient CreateAuthenticatedClient(Guid userId, string email, string displayName)
    {
        var token = GenerateTestToken(userId, email, displayName);
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

    public static string GenerateTestToken(Guid userId, string email, string displayName)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestJwtKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(ClaimTypes.Name, displayName),
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
        await _postgres.DisposeAsync();
    }
}
