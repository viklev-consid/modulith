using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Modules.Audit.Persistence;
using Modulith.Modules.Catalog.Persistence;
using Modulith.Modules.Notifications.Domain;
using Modulith.Modules.Notifications.Persistence;
using Modulith.Modules.Users.Persistence;
using Modulith.Shared.Infrastructure.Notifications;
using Modulith.Shared.Kernel.Interfaces;
using Modulith.TestSupport;
using Modulith.TestSupport.Fakes;
using Npgsql;
using Wolverine;
using Wolverine.Tracking;

namespace Modulith.Modules.Catalog.IntegrationTests.Integration;

// ── Retry-policy fixture ────────────────────────────────────────────────────────────────────────

[CollectionDefinition("WolverineRetryPolicy")]
public sealed class WolverineRetryPolicyCollection : ICollectionFixture<WolverineRetryPolicyFixture> { }

/// <summary>
/// Fixture for Wolverine retry-policy integration tests.
/// Injects a <see cref="FlakyEmailSender"/> that throws <see cref="IOException"/> on the first
/// SMTP attempt and succeeds on the second. The <see cref="TestClock"/> is advanced by 6 minutes
/// before the exception is thrown, which exercises the <em>stale-row crash-recovery path</em> in
/// <c>NotificationSendGuard.TryClaimAsync</c> (rows stuck in Sending for longer than 5 min are
/// reset to Pending and re-claimed).
/// <para>
/// For the primary transient-recovery path — where <c>MarkReadyAsync</c> resets the row
/// immediately in the handler's <c>catch (IOException)</c> block without any clock advance — see
/// <c>NotificationsSendGuardRecoveryTests</c> in the Notifications integration test project.
/// </para>
/// </summary>
public sealed class WolverineRetryPolicyFixture : ApiTestFixture
{
    internal TestClock TestClock { get; } = new TestClock();
    internal FlakyEmailSender FlakyEmail { get; private set; } = null!;

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        // Advance must exceed NotificationSendGuard.StuckSendingThreshold (5 min).
        FlakyEmail = new FlakyEmailSender(TestClock, TimeSpan.FromMinutes(6));
        services.AddSingleton<IClock>(TestClock);
        services.AddSingleton<IEmailSender>(FlakyEmail);
    }

    protected override async Task MigrateAsync(IServiceProvider services)
    {
        await services.GetRequiredService<UsersDbContext>().Database.MigrateAsync();
        await services.GetRequiredService<CatalogDbContext>().Database.MigrateAsync();
        await services.GetRequiredService<AuditDbContext>().Database.MigrateAsync();
        await services.GetRequiredService<NotificationsDbContext>().Database.MigrateAsync();
    }

    protected override string[] GetSchemasToReset() => ["users", "catalog", "audit", "notifications"];
}

// ── Dead-letter fixture ─────────────────────────────────────────────────────────────────────────

[CollectionDefinition("WolverineDeadLetter")]
public sealed class WolverineDeadLetterCollection : ICollectionFixture<WolverineDeadLetterFixture> { }

/// <summary>
/// Fixture for Wolverine dead-letter integration tests.
/// Replaces <see cref="IConsentRegistry"/> with a double that throws
/// <see cref="InvalidOperationException"/> on every call. This triggers the explicit
/// <c>InvalidOperationException → MoveToErrorQueue</c> policy configured in <c>Program.cs</c>,
/// moving the Notifications envelope to <c>wolverine.wolverine_dead_letters</c> without retries.
/// </summary>
public sealed class WolverineDeadLetterFixture : ApiTestFixture
{
    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.AddSingleton<IEmailSender, FakeEmailSender>();
        services.AddSingleton<IConsentRegistry, AlwaysThrowConsentRegistry>();
    }

    protected override async Task MigrateAsync(IServiceProvider services)
    {
        await services.GetRequiredService<UsersDbContext>().Database.MigrateAsync();
        await services.GetRequiredService<CatalogDbContext>().Database.MigrateAsync();
        await services.GetRequiredService<AuditDbContext>().Database.MigrateAsync();
        await services.GetRequiredService<NotificationsDbContext>().Database.MigrateAsync();
    }

    protected override string[] GetSchemasToReset() => ["users", "catalog", "audit", "notifications"];
}

/// <summary>
/// Test double for <see cref="IConsentRegistry"/> that always throws
/// <see cref="InvalidOperationException"/>. Used to drive the
/// <c>InvalidOperationException → MoveToErrorQueue</c> Wolverine error policy in
/// the Notifications handler before any state is committed (no side effects to undo).
/// </summary>
public sealed class AlwaysThrowConsentRegistry : IConsentRegistry
{
    public Task<bool> HasConsentedAsync(Guid userId, string consentKey, CancellationToken ct = default) =>
        throw new InvalidOperationException("Consent registry unavailable — error-policy test double");

    // Registration still works: Users module adds consent directly via db.Consents.Add, not
    // via IConsentRegistry.GrantAsync. GrantAsync / RevokeAsync are no-ops here.
    public Task GrantAsync(Guid userId, string consentKey, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task RevokeAsync(Guid userId, string consentKey, CancellationToken ct = default) =>
        Task.CompletedTask;
}

// ── Test 1: transient retry ─────────────────────────────────────────────────────────────────────

/// <summary>
/// Proves the <c>IOException → RetryWithCooldown</c> policy: a transient SMTP connection failure
/// causes Wolverine to retry the Notifications handler, and after the retry the email is delivered
/// successfully with no dead letters produced.
/// </summary>
[Collection("WolverineRetryPolicy")]
[Trait("Category", "Integration")]
public sealed class TransientRetryPolicyTests(WolverineRetryPolicyFixture fixture) : IAsyncLifetime
{
    private readonly HttpClient _client = fixture.CreateAnonymousClient();

    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task TransientSmtpFailure_RetriesAndDelivers_WithZeroDeadLetters()
    {
        // Arrange
        var request = new { Email = "retry-policy@example.com", Password = "Password1!", DisplayName = "Retry Test" };

        // Act — TrackActivity waits for all cascading messages to settle, including the
        // Wolverine retry that fires after the IOException retry cooldown (5 s).
        // FlakyEmailSender: attempt 1 throws IOException and advances TestClock by 6 min;
        // attempt 2 succeeds. The clock advance causes NotificationSendGuard to treat the
        // Sending row as stale and re-claim it on the retry.
        //
        // DoNotAssertOnExceptionsDetected is required because TrackActivity records the
        // intermediate IOException even though the message ultimately succeeds.
        Func<IMessageContext, Task> act = async _ =>
        {
            var response = await _client.PostAsJsonAsync("/v1/users/register", request);
            response.EnsureSuccessStatusCode();
        };
        await fixture.ApplicationHost.TrackActivity()
            .DoNotAssertOnExceptionsDetected()
            .Timeout(TimeSpan.FromSeconds(30))
            .ExecuteAndWaitAsync(act);

        // Assert — email sent exactly once (on the successful retry)
        Assert.Single(fixture.FlakyEmail.SentMessages);
        Assert.Equal(2, fixture.FlakyEmail.TotalAttempts);

        // Assert — no dead letters
        await using var conn = new NpgsqlConnection(fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM wolverine.wolverine_dead_letters";
        var dead = (long)(await cmd.ExecuteScalarAsync())!;
        Assert.Equal(0L, dead);

        // Assert — notification log marked Sent
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        var log = await db.NotificationLogs
            .SingleAsync(l => l.RecipientEmail == "retry-policy@example.com");
        Assert.Equal(NotificationDeliveryStatus.Sent, log.DeliveryStatus);
    }
}

// ── Test 2: non-recoverable dead-letter ────────────────────────────────────────────────────────

/// <summary>
/// Proves the <c>InvalidOperationException → MoveToErrorQueue</c> policy: a non-recoverable
/// failure in the Notifications handler moves the envelope directly to the dead-letter table
/// without any retry attempts.
/// </summary>
[Collection("WolverineDeadLetter")]
[Trait("Category", "Integration")]
public sealed class DeadLetterPolicyTests(WolverineDeadLetterFixture fixture) : IAsyncLifetime
{
    private readonly HttpClient _client = fixture.CreateAnonymousClient();

    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task NonRecoverableFailure_MovesDirectlyToDeadLetterQueue()
    {
        // Arrange
        var request = new { Email = "dead-letter-policy@example.com", Password = "Password1!", DisplayName = "Dead Letter Test" };

        // Act — AlwaysThrowConsentRegistry throws InvalidOperationException before the
        // Notifications handler writes any state. The MoveToErrorQueue policy fires
        // immediately (no retries), placing exactly one envelope in the dead-letter table.
        Func<IMessageContext, Task> act = async _ =>
        {
            var response = await _client.PostAsJsonAsync("/v1/users/register", request);
            response.EnsureSuccessStatusCode();
        };
        await fixture.ApplicationHost.TrackActivity()
            .DoNotAssertOnExceptionsDetected()
            .Timeout(TimeSpan.FromSeconds(15))
            .ExecuteAndWaitAsync(act);

        // Assert — exactly one dead letter (the Notifications handler envelope)
        await using var conn = new NpgsqlConnection(fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM wolverine.wolverine_dead_letters";
        var dead = (long)(await cmd.ExecuteScalarAsync())!;
        Assert.Equal(1L, dead);

        // Assert — no email attempted (handler never reached emailSender.SendAsync)
        using var scope = fixture.Services.CreateScope();
        var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>() as FakeEmailSender;
        Assert.NotNull(emailSender);
        Assert.Empty(emailSender.SentMessages);

        // Assert — no notification log row written (handler threw before SaveChangesAsync)
        var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        var logCount = await db.NotificationLogs.CountAsync();
        Assert.Equal(0, logCount);
    }
}
