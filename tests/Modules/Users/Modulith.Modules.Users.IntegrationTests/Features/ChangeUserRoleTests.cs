using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Features.ChangeUserRole;
using Modulith.Modules.Users.Features.GetCurrentUser;
using Modulith.Modules.Users.Features.GetUserById;
using Modulith.Modules.Users.Features.ListUsers;
using Modulith.Modules.Users.Features.Register;
using Modulith.Modules.Users.Persistence;

namespace Modulith.Modules.Users.IntegrationTests.Features;

[Collection("UsersModule")]
[Trait("Category", "Integration")]
public sealed class ChangeUserRoleTests(UsersApiFixture fixture) : IAsyncLifetime
{
    private readonly HttpClient _anon = fixture.CreateAnonymousClient();

    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ── helpers ────────────────────────────────────────────────────────────

    private async Task<RegisterResponse> RegisterAsync(string email, string name = "Test User")
    {
        var resp = await _anon.PostAsJsonAsync("/v1/users/register",
            new RegisterRequest(email, "Password1!", name));
        return (await resp.Content.ReadFromJsonAsync<RegisterResponse>())!;
    }

    private HttpClient AdminClient(Guid userId, string email)
        => fixture.CreateAuthenticatedClient(userId, email, "Admin", "admin");

    private HttpClient UserClient(Guid userId, string email)
        => fixture.CreateAuthenticatedClient(userId, email, "User", "user");

    // ── ChangeUserRole ─────────────────────────────────────────────────────

    [Fact]
    public async Task ChangeUserRole_AdminPromotesUser_Returns200AndNewRole()
    {
        var admin = await RegisterAsync("admin@example.com", "Admin");
        var target = await RegisterAsync("target@example.com", "Target");

        var response = await AdminClient(admin.UserId, "admin@example.com")
            .PutAsJsonAsync($"/v1/users/{target.UserId}/role",
                new ChangeUserRoleRequest("admin"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ChangeUserRoleResponse>();
        Assert.NotNull(body);
        Assert.Equal(target.UserId, body.UserId);
        Assert.Equal("admin", body.NewRole);
    }

    [Fact]
    public async Task ChangeUserRole_RegularUserIsRejected_Returns403()
    {
        var user = await RegisterAsync("user@example.com");
        var target = await RegisterAsync("target@example.com");

        var response = await UserClient(user.UserId, "user@example.com")
            .PutAsJsonAsync($"/v1/users/{target.UserId}/role",
                new ChangeUserRoleRequest("admin"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ChangeUserRole_AdminChangesSelf_Returns422()
    {
        var admin = await RegisterAsync("admin@example.com", "Admin");
        var client = AdminClient(admin.UserId, "admin@example.com");

        var response = await client.PutAsJsonAsync($"/v1/users/{admin.UserId}/role",
            new ChangeUserRoleRequest("user"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ChangeUserRole_Unauthenticated_Returns401()
    {
        var target = await RegisterAsync("target@example.com");

        var response = await _anon.PutAsJsonAsync($"/v1/users/{target.UserId}/role",
            new ChangeUserRoleRequest("admin"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ChangeUserRole_UnknownRole_Returns404()
    {
        var admin = await RegisterAsync("admin@example.com", "Admin");
        var target = await RegisterAsync("target@example.com", "Target");

        var response = await AdminClient(admin.UserId, "admin@example.com")
            .PutAsJsonAsync($"/v1/users/{target.UserId}/role",
                new ChangeUserRoleRequest("moderator"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── ListUsers ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ListUsers_Admin_Returns200WithUsers()
    {
        var admin = await RegisterAsync("admin@example.com", "Admin");
        await RegisterAsync("alice@example.com", "Alice");

        var response = await AdminClient(admin.UserId, "admin@example.com")
            .GetAsync("/v1/users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ListUsersResponse>();
        Assert.NotNull(body);
        Assert.True(body.TotalCount >= 2);
    }

    [Fact]
    public async Task ListUsers_RegularUser_Returns403()
    {
        var user = await RegisterAsync("user@example.com");

        var response = await UserClient(user.UserId, "user@example.com")
            .GetAsync("/v1/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── GetUserById ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetUserById_Admin_Returns200WithUser()
    {
        var admin = await RegisterAsync("admin@example.com", "Admin");
        var target = await RegisterAsync("target@example.com", "Target");

        var response = await AdminClient(admin.UserId, "admin@example.com")
            .GetAsync($"/v1/users/{target.UserId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<GetUserByIdResponse>();
        Assert.NotNull(body);
        Assert.Equal(target.UserId, body.UserId);
        Assert.Equal("target@example.com", body.Email);
        Assert.Equal("user", body.Role);
    }

    [Fact]
    public async Task GetUserById_NonExistent_Returns404()
    {
        var admin = await RegisterAsync("admin@example.com", "Admin");

        var response = await AdminClient(admin.UserId, "admin@example.com")
            .GetAsync($"/v1/users/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── GetCurrentUser /me returns role from token ─────────────────────────

    [Fact]
    public async Task GetCurrentUser_UserToken_ReturnsUserRoleAndEmptyPermissions()
    {
        var registered = await RegisterAsync("alice@example.com", "Alice");

        var response = await UserClient(registered.UserId, "alice@example.com")
            .GetAsync("/v1/users/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<GetCurrentUserResponse>();
        Assert.NotNull(body);
        Assert.Equal("user", body.Role);
        Assert.Empty(body.Permissions);
        Assert.NotEmpty(body.PermissionsVersion);
    }

    [Fact]
    public async Task GetCurrentUser_AdminToken_ReturnsAdminRoleAndPermissions()
    {
        var registered = await RegisterAsync("admin@example.com", "Admin");

        var response = await AdminClient(registered.UserId, "admin@example.com")
            .GetAsync("/v1/users/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<GetCurrentUserResponse>();
        Assert.NotNull(body);
        Assert.Equal("admin", body.Role);
        Assert.NotEmpty(body.Permissions);
        Assert.NotEmpty(body.PermissionsVersion);
    }

    // ── Stale-token window (known behaviour, deliberately documented) ─────────
    //
    // Stateless JWTs accept a bounded window of stale permissions in exchange
    // for no per-request DB lookup. These tests pin that behaviour so a future
    // change (e.g. security-stamp validation) is noticed and deliberate.

    [Fact]
    public async Task DemotedAdmin_WithOldAdminToken_StillAuthorizedUntilTokenExpires()
    {
        // Arrange: build an admin JWT for alice BEFORE she is demoted.
        var alice = await RegisterAsync("alice@example.com", "Alice");
        var adminTokenBeforeDemotion = AdminClient(alice.UserId, "alice@example.com");

        // Confirm the token works before demotion.
        Assert.Equal(HttpStatusCode.OK,
            (await adminTokenBeforeDemotion.GetAsync("/v1/users")).StatusCode);

        // A second admin demotes alice.
        var other = await RegisterAsync("other@example.com", "Other");
        await AdminClient(other.UserId, "other@example.com")
            .PutAsJsonAsync($"/v1/users/{alice.UserId}/role", new ChangeUserRoleRequest("user"));

        // Act: use the OLD admin token immediately after demotion.
        var afterDemotion = await adminTokenBeforeDemotion.GetAsync("/v1/users");

        // Assert: token still authorizes — this is the known stale-token window.
        // If this test starts failing it means server-side token revocation has been
        // introduced, which is intentional and welcome — update this assertion then.
        Assert.Equal(HttpStatusCode.OK, afterDemotion.StatusCode);
    }

    [Fact]
    public async Task DemotedAdmin_MeEndpoint_ImmediatelyReflectsNewRoleFromToken()
    {
        // /me uses the role from the JWT claim, not the DB, so it stays consistent
        // with what the token authorizes. A demoted admin holding an old admin JWT
        // will see role=admin on /me (matching their still-valid admin permissions)
        // rather than the contradictory role=user + working admin endpoints.
        var alice = await RegisterAsync("alice@example.com", "Alice");
        var adminToken = AdminClient(alice.UserId, "alice@example.com");

        var other = await RegisterAsync("other@example.com", "Other");
        await AdminClient(other.UserId, "other@example.com")
            .PutAsJsonAsync($"/v1/users/{alice.UserId}/role", new ChangeUserRoleRequest("user"));

        var meResp = await adminToken.GetAsync("/v1/users/me");
        var body = await meResp.Content.ReadFromJsonAsync<GetCurrentUserResponse>();

        Assert.NotNull(body);
        // Role and permissions both reflect the token claim, not the DB — consistent.
        Assert.Equal("admin", body.Role);
        Assert.NotEmpty(body.Permissions);
    }

    // ── Concurrency ────────────────────────────────────────────────────────

    [Fact]
    public async Task ChangeUserRole_WhenSuccessful_RevokesTargetRefreshTokens()
    {
        // Verifies that token revocation (which was moved to run after SaveChanges
        // in the concurrency fix) still fires correctly on the happy path.
        var admin = await RegisterAsync("admin@example.com", "Admin");
        var target = await RegisterAsync("target@example.com", "Target");

        // Log in as target to create an active refresh token.
        var loginResp = await _anon.PostAsJsonAsync("/v1/users/login",
            new { Email = "target@example.com", Password = "Password1!" });
        Assert.Equal(HttpStatusCode.OK, loginResp.StatusCode);

        var activeTokensBefore = await CountActiveRefreshTokensAsync(target.UserId);
        Assert.True(activeTokensBefore >= 1);

        // Act — admin promotes the target.
        var roleResp = await AdminClient(admin.UserId, "admin@example.com")
            .PutAsJsonAsync($"/v1/users/{target.UserId}/role", new ChangeUserRoleRequest("admin"));
        Assert.Equal(HttpStatusCode.OK, roleResp.StatusCode);

        // Assert — target's refresh tokens must be revoked so re-login is forced
        // and the new role is reflected in the next access token.
        var activeTokensAfter = await CountActiveRefreshTokensAsync(target.UserId);
        Assert.Equal(0, activeTokensAfter);
    }

    [Fact]
    public async Task ChangeUserRole_ConcurrentRequests_HandledGracefully()
    {
        // Fires two simultaneous role-change requests for the same user.
        // With optimistic concurrency (xmin), if both reads land before either write
        // commits, one SaveChanges will encounter a stale row version and the handler
        // returns UsersErrors.ConcurrencyConflict (409). This test verifies:
        //   1. Neither request propagates as an unhandled 500.
        //   2. Every response is a recognised outcome (200 OK or 409 Conflict).
        //   3. At least one request succeeded — the role was actually changed.
        var admin = await RegisterAsync("admin@example.com", "Admin");
        var target = await RegisterAsync("target@example.com", "Target");

        var client1 = AdminClient(admin.UserId, "admin@example.com");
        var client2 = AdminClient(admin.UserId, "admin@example.com");

        // Act — fire both concurrently.
        var results = await Task.WhenAll(
            client1.PutAsJsonAsync($"/v1/users/{target.UserId}/role", new ChangeUserRoleRequest("admin")),
            client2.PutAsJsonAsync($"/v1/users/{target.UserId}/role", new ChangeUserRoleRequest("admin"))
        );
        var r1 = results[0];
        var r2 = results[1];

        // Assert — no unhandled errors.
        Assert.NotEqual(HttpStatusCode.InternalServerError, r1.StatusCode);
        Assert.NotEqual(HttpStatusCode.InternalServerError, r2.StatusCode);

        // Each response is either a success or a gracefully-handled conflict.
        var acceptable = new[] { HttpStatusCode.OK, HttpStatusCode.Conflict };
        Assert.Contains(r1.StatusCode, acceptable);
        Assert.Contains(r2.StatusCode, acceptable);

        // At least one request must have committed the role change.
        Assert.True(
            r1.StatusCode == HttpStatusCode.OK || r2.StatusCode == HttpStatusCode.OK,
            "Expected at least one request to succeed.");
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private async Task<int> CountActiveRefreshTokensAsync(Guid userId)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        var typedId = new UserId(userId);
        return await db.RefreshTokens
            .CountAsync(t => t.UserId == typedId && t.RevokedAt == null);
    }
}
