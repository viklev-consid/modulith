using System.Net;
using System.Net.Http.Json;
using Modulith.Modules.Users.Features.ChangeUserRole;
using Modulith.Modules.Users.Features.GetCurrentUser;
using Modulith.Modules.Users.Features.GetUserById;
using Modulith.Modules.Users.Features.ListUsers;
using Modulith.Modules.Users.Features.Register;

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

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task ChangeUserRole_Unauthenticated_Returns401()
    {
        var target = await RegisterAsync("target@example.com");

        var response = await _anon.PutAsJsonAsync($"/v1/users/{target.UserId}/role",
            new ChangeUserRoleRequest("admin"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
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

    // ── GetCurrentUser /me returns role + permissions ──────────────────────

    [Fact]
    public async Task GetCurrentUser_RegisteredUser_ReturnsUserRoleAndEmptyPermissions()
    {
        var registered = await RegisterAsync("alice@example.com", "Alice");
        var client = UserClient(registered.UserId, "alice@example.com");

        var response = await client.GetAsync("/v1/users/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<GetCurrentUserResponse>();
        Assert.NotNull(body);
        Assert.Equal("user", body.Role);
        Assert.Empty(body.Permissions);
        Assert.NotEmpty(body.PermissionsVersion);
    }

    [Fact]
    public async Task GetCurrentUser_AdminUser_ReturnsAdminRoleAndPermissions()
    {
        var registered = await RegisterAsync("admin@example.com", "Admin");

        // Promote to admin via role-change endpoint (use admin JWT for the caller too,
        // since we're testing the /me response shape, not the promotion path).
        // Promote via a fake admin caller who promotes this user.
        var promoter = await RegisterAsync("promoter@example.com", "Promoter");
        await AdminClient(promoter.UserId, "promoter@example.com")
            .PutAsJsonAsync($"/v1/users/{registered.UserId}/role",
                new ChangeUserRoleRequest("admin"));

        // Now the target is an admin in the DB. Use a JWT that carries admin role.
        var adminClient = AdminClient(registered.UserId, "admin@example.com");
        var meResp = await adminClient.GetAsync("/v1/users/me");

        Assert.Equal(HttpStatusCode.OK, meResp.StatusCode);
        var body = await meResp.Content.ReadFromJsonAsync<GetCurrentUserResponse>();
        Assert.NotNull(body);
        Assert.Equal("admin", body.Role);
        Assert.NotEmpty(body.Permissions);
        Assert.NotEmpty(body.PermissionsVersion);
    }
}
