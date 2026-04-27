using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Modules.Audit.Persistence;
using Modulith.Modules.Users.Contracts.Events;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Features.ExternalLogin.Google.Confirm;
using Modulith.Modules.Users.Features.Register;
using Modulith.Modules.Users.Features.ExternalLogin.Google.Login;
using Modulith.Modules.Users.Persistence;
using Modulith.Shared.Kernel.Interfaces;
using Npgsql;
using Wolverine.Tracking;

namespace Modulith.Modules.Users.IntegrationTests.Features;

[Collection("GoogleUsersModule")]
[Trait("Category", "Integration")]
public sealed class GoogleLoginConfirmTests(GoogleUsersApiFixture fixture) : IAsyncLifetime
{
    private readonly HttpClient _client = fixture.CreateAnonymousClient();

    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GoogleLoginConfirm_ForNewUser_Returns200AndCreatesUser()
    {
        const string email = "newgoogle@example.com";
        const string subject = "sub-new-confirm";

        var rawToken = await SeedPendingLoginAsync(subject, email, isExistingUser: false);

        var response = await _client.PostAsJsonAsync("/v1/users/auth/google/confirm",
            new GoogleLoginConfirmRequest(rawToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<GoogleLoginConfirmResponse>();
        Assert.NotNull(body);
        Assert.True(body.IsNewUser);
        Assert.NotEmpty(body.AccessToken);
        Assert.NotEmpty(body.RefreshToken);
        Assert.NotEqual(Guid.Empty, body.UserId);
    }

    [Fact]
    public async Task GoogleLoginConfirm_ForNewUser_PersistsUserInDatabase()
    {
        const string email = "newgoogle2@example.com";
        const string subject = "sub-new-confirm2";

        var rawToken = await SeedPendingLoginAsync(subject, email, isExistingUser: false);

        await _client.PostAsJsonAsync("/v1/users/auth/google/confirm",
            new GoogleLoginConfirmRequest(rawToken));

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        var emailVal = Email.Create(email).Value;
        var user = await db.Users
            .Include(u => u.ExternalLogins)
            .FirstOrDefaultAsync(u => u.Email == emailVal);
        Assert.NotNull(user);
        Assert.Single(user.ExternalLogins);
        Assert.Equal("Google", user.ExternalLogins[0].Provider.ToString());
        Assert.Equal(subject, user.ExternalLogins[0].Subject);
    }

    [Fact]
    public async Task GoogleLoginConfirm_ForExistingUser_Returns200AndLinksGoogle()
    {
        const string email = "existinglinked@example.com";
        const string subject = "sub-existing-confirm";

        await _client.PostAsJsonAsync("/v1/users/register",
            new RegisterRequest(email, "Password1!", "Alice"));

        var rawToken = await SeedPendingLoginAsync(subject, email, isExistingUser: true);

        var response = await _client.PostAsJsonAsync("/v1/users/auth/google/confirm",
            new GoogleLoginConfirmRequest(rawToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<GoogleLoginConfirmResponse>();
        Assert.NotNull(body);
        Assert.False(body.IsNewUser);
        Assert.NotEmpty(body.AccessToken);
    }

    [Fact]
    public async Task GoogleLoginConfirm_ForExistingUser_LinksGoogleInDatabase()
    {
        const string email = "existinglinked2@example.com";
        const string subject = "sub-existing-confirm2";

        await _client.PostAsJsonAsync("/v1/users/register",
            new RegisterRequest(email, "Password1!", "Alice"));

        var rawToken = await SeedPendingLoginAsync(subject, email, isExistingUser: true);

        await _client.PostAsJsonAsync("/v1/users/auth/google/confirm",
            new GoogleLoginConfirmRequest(rawToken));

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        var emailVal = Email.Create(email).Value;
        var user = await db.Users
            .Include(u => u.ExternalLogins)
            .FirstAsync(u => u.Email == emailVal);
        Assert.Single(user.ExternalLogins);
        Assert.Equal(subject, user.ExternalLogins[0].Subject);
    }

    [Fact]
    public async Task GoogleLoginConfirm_AfterLinking_SubsequentGoogleLoginUsesLinkedFastPath()
    {
        const string email = "fastpathafter@example.com";
        const string subject = "sub-fastpath-confirm";

        var rawToken = await SeedPendingLoginAsync(subject, email, isExistingUser: false);
        await _client.PostAsJsonAsync("/v1/users/auth/google/confirm",
            new GoogleLoginConfirmRequest(rawToken));

        // Now the Google account is linked — fast path should return 200
        fixture.GoogleVerifier.SetIdentity(subject, email, "Test User");
        var loginResp = await _client.PostAsJsonAsync("/v1/users/auth/google/login",
            new GoogleLoginRequest("any-token"));

        Assert.Equal(HttpStatusCode.OK, loginResp.StatusCode);
        var body = await loginResp.Content.ReadFromJsonAsync<GoogleLoginResponse>();
        Assert.NotNull(body);
        Assert.False(body.IsPending);
    }

    [Fact]
    public async Task GoogleLoginConfirm_WithExpiredToken_Returns422()
    {
        const string email = "expired@example.com";
        const string subject = "sub-expired";

        var rawToken = await SeedPendingLoginAsync(subject, email, isExistingUser: false, lifetime: TimeSpan.FromSeconds(-1));

        var response = await _client.PostAsJsonAsync("/v1/users/auth/google/confirm",
            new GoogleLoginConfirmRequest(rawToken));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GoogleLoginConfirm_WithAlreadyConsumedToken_Returns422()
    {
        const string email = "consumed@example.com";
        const string subject = "sub-consumed";

        var rawToken = await SeedPendingLoginAsync(subject, email, isExistingUser: false);

        // Consume once successfully (user gets created)
        await _client.PostAsJsonAsync("/v1/users/auth/google/confirm",
            new GoogleLoginConfirmRequest(rawToken));

        // Second attempt must fail
        var response = await _client.PostAsJsonAsync("/v1/users/auth/google/confirm",
            new GoogleLoginConfirmRequest(rawToken));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GoogleLoginConfirm_WithUnknownToken_Returns422()
    {
        var response = await _client.PostAsJsonAsync("/v1/users/auth/google/confirm",
            new GoogleLoginConfirmRequest("totally-unknown-token-value"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GoogleLoginConfirm_WithEmptyToken_Returns422()
    {
        var response = await _client.PostAsJsonAsync("/v1/users/auth/google/confirm",
            new GoogleLoginConfirmRequest(""));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task GoogleLoginConfirm_WhenTokenExceedsMaxLength_Returns422()
    {
        var oversized = new string('a', 65);

        var response = await _client.PostAsJsonAsync("/v1/users/auth/google/confirm",
            new GoogleLoginConfirmRequest(oversized));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task GoogleLoginConfirm_WhenUserRegisteredAfterLoginInitiated_LinksToExistingUser()
    {
        const string email = "latecreated@example.com";
        const string subject = "sub-late-created";

        // Pending created with isExistingUser=false — user didn't exist at login time.
        var rawToken = await SeedPendingLoginAsync(subject, email, isExistingUser: false);

        // User registers with a password *after* the pending record was created.
        await _client.PostAsJsonAsync("/v1/users/register",
            new RegisterRequest(email, "Password1!", "Alice"));

        // Confirm should link Google to the now-existing account, not try to provision a new one.
        var response = await _client.PostAsJsonAsync("/v1/users/auth/google/confirm",
            new GoogleLoginConfirmRequest(rawToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<GoogleLoginConfirmResponse>();
        Assert.NotNull(body);
        Assert.False(body.IsNewUser);
        Assert.NotEmpty(body.AccessToken);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        var emailVal = Email.Create(email).Value;
        var user = await db.Users
            .Include(u => u.ExternalLogins)
            .FirstAsync(u => u.Email == emailVal);
        Assert.Single(user.ExternalLogins);
        Assert.Equal(subject, user.ExternalLogins[0].Subject);
    }

    [Fact]
    public async Task GoogleLoginConfirm_WhenRegistrationCommitsDuringProvision_RetriesExistingUserLinkPath()
    {
        const string email = "latecollision@example.com";
        const string subject = "sub-late-collision";

        var rawToken = await SeedPendingLoginAsync(subject, email, isExistingUser: false);
        var connectionString = GetUsersConnectionString();

        await InstallExternalProvisionDelayTriggerAsync(connectionString, email);

        try
        {
            var confirmTask = _client.PostAsJsonAsync(
                "/v1/users/auth/google/confirm",
                new GoogleLoginConfirmRequest(rawToken));

            await WaitForExternalProvisionInsertAsync(connectionString);

            var registerResponse = await _client.PostAsJsonAsync(
                "/v1/users/register",
                new RegisterRequest(email, "Password1!", "Alice"));

            Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

            var confirmResponse = await confirmTask;

            Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);

            var body = await confirmResponse.Content.ReadFromJsonAsync<GoogleLoginConfirmResponse>();
            Assert.NotNull(body);
            Assert.False(body.IsNewUser);
            Assert.NotEmpty(body.AccessToken);

            using var scope = fixture.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
            var emailVal = Email.Create(email).Value;
            var users = await db.Users
                .Include(u => u.ExternalLogins)
                .Where(u => u.Email == emailVal)
                .ToListAsync();

            Assert.Single(users);
            Assert.Single(users[0].ExternalLogins);
            Assert.Equal(subject, users[0].ExternalLogins[0].Subject);
        }
        finally
        {
            await RemoveExternalProvisionDelayTriggerAsync(connectionString);
        }
    }

    private async Task<string> SeedPendingLoginAsync(
        string subject, string email, bool isExistingUser,
        TimeSpan? lifetime = null)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var (pending, rawToken) = PendingExternalLogin.Create(
            ExternalLoginProvider.Google, subject, email, "Test User",
            isExistingUser, createdFromIp: null, userAgent: null,
            lifetime ?? TimeSpan.FromMinutes(15), clock);

        db.PendingExternalLogins.Add(pending);
        await db.SaveChangesAsync();
        return rawToken;
    }

    [Fact]
    public async Task GoogleLoginConfirm_ForNewUser_PublishesUserLoggedInEvent()
    {
        // Verifies that the new-user provisioning path emits UserLoggedInV1 so the first
        // external sign-in appears in the audit trail alongside UserProvisionedFromExternalV1.
        const string email = "newgoogle-event@example.com";
        const string subject = "sub-new-confirm-event";

        var rawToken = await SeedPendingLoginAsync(subject, email, isExistingUser: false);

        HttpResponseMessage? response = null;
        await fixture.ApplicationHost
            .TrackActivity()
            .Timeout(TimeSpan.FromSeconds(15))
            .WaitForMessageToBeReceivedAt<UserLoggedInV1>(fixture.ApplicationHost)
            .ExecuteAndWaitAsync((Func<Wolverine.IMessageContext, Task>)(async _ =>
            {
                response = await _client.PostAsJsonAsync("/v1/users/auth/google/confirm",
                    new GoogleLoginConfirmRequest(rawToken));
            }));

        Assert.Equal(HttpStatusCode.OK, response!.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<GoogleLoginConfirmResponse>();
        Assert.NotNull(body);

        using var auditScope = fixture.Services.CreateScope();
        var auditDb = auditScope.ServiceProvider.GetRequiredService<AuditDbContext>();
        var entry = await auditDb.AuditEntries.FirstOrDefaultAsync(e =>
            e.EventType == "user.logged_in" && e.ActorId == body.UserId);

        Assert.NotNull(entry);
    }

    private string GetUsersConnectionString()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        return db.Database.GetConnectionString() ?? throw new InvalidOperationException("Users test connection string is not configured.");
    }

    private static async Task InstallExternalProvisionDelayTriggerAsync(string connectionString, string email)
    {
        var escapedEmail = email.Replace("'", "''", StringComparison.Ordinal);

        const string triggerName = "tr_test_delay_external_user_insert";
        const string functionName = "users.test_delay_external_user_insert";

        var sql = $"""
            DROP TRIGGER IF EXISTS {triggerName} ON users.users;
            DROP FUNCTION IF EXISTS {functionName}();

            CREATE FUNCTION {functionName}()
            RETURNS trigger
            LANGUAGE plpgsql
            AS $$
            BEGIN
                IF NEW.email = '{escapedEmail}' AND NEW.password_hash IS NULL THEN
                    PERFORM pg_sleep(3);
                END IF;

                RETURN NEW;
            END;
            $$;

            CREATE TRIGGER {triggerName}
            BEFORE INSERT ON users.users
            FOR EACH ROW
            EXECUTE FUNCTION {functionName}();
            """;

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task RemoveExternalProvisionDelayTriggerAsync(string connectionString)
    {
        const string sql = """
            DROP TRIGGER IF EXISTS tr_test_delay_external_user_insert ON users.users;
            DROP FUNCTION IF EXISTS users.test_delay_external_user_insert();
            """;

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task WaitForExternalProvisionInsertAsync(string connectionString)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM pg_stat_activity
                WHERE pid <> pg_backend_pid()
                  AND datname = current_database()
                  AND state = 'active'
                  AND query ~* 'insert\s+into\s+"?users"?\."?users"?'
            );
            """;

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        for (var attempt = 0; attempt < 100; attempt++)
        {
            await using var cmd = new NpgsqlCommand(sql, conn);
            var result = await cmd.ExecuteScalarAsync();
            if (result is true)
            {
                return;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException("Timed out waiting for GoogleLoginConfirm provisioning insert to start.");
    }
}
