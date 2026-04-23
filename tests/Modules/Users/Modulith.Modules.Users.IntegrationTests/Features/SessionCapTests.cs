using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Modules.Users.Features.Login;
using Modulith.Modules.Users.Features.Register;
using Modulith.Modules.Users.Persistence;

namespace Modulith.Modules.Users.IntegrationTests.Features;

/// <summary>
/// Fixture that lowers MaxActiveRefreshTokensPerUser to 2 so cap enforcement
/// can be tested without issuing N+1 tokens for the default of 10.
/// </summary>
public sealed class SmallCapFixture : UsersApiFixture
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Modules:Users:MaxActiveRefreshTokensPerUser", "2");
    }
}

[Trait("Category", "Integration")]
public sealed class SessionCapTests(SmallCapFixture fixture) : IClassFixture<SmallCapFixture>, IAsyncLifetime
{
    private readonly HttpClient _client = fixture.CreateAnonymousClient();

    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Login_WhenCapReached_RevokesOldestTokenAndKeepsCount()
    {
        // Arrange — register once, then login three times (cap = 2).
        await _client.PostAsJsonAsync("/v1/users/register",
            new RegisterRequest("alice@example.com", "Password1!", "Alice"));

        _ = await LoginAsync();
        _ = await LoginAsync();
        LoginResponse login3 = await LoginAsync(); // should push out login1's token

        // Act — inspect persisted state directly.
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();

        var all = await db.RefreshTokens.OrderBy(t => t.IssuedAt).ToListAsync();
        var active = all.Where(t => t.RevokedAt == null).ToList();
        var revoked = all.Where(t => t.RevokedAt != null).ToList();

        // Assert — exactly the cap number of active tokens; the oldest was revoked.
        Assert.Equal(2, active.Count);
        Assert.Single(revoked);

        // The revoked token must be older than either active token.
        Assert.True(revoked[0].IssuedAt <= active.Min(t => t.IssuedAt),
            "The revoked token should be the oldest one.");

        // The third login must have succeeded — cap enforcement is not a login failure.
        Assert.NotEmpty(login3.RefreshToken);
    }

    private async Task<LoginResponse> LoginAsync()
    {
        var response = await _client.PostAsJsonAsync("/v1/users/login",
            new LoginRequest("alice@example.com", "Password1!"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(body);
        return body;
    }
}
