using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Jobs;
using Modulith.Modules.Users.Persistence;
using Modulith.Shared.Kernel.Interfaces;
using Wolverine;

namespace Modulith.Modules.Users.IntegrationTests.Jobs;

[Collection("UsersModule")]
[Trait("Category", "Integration")]
public sealed class SweepExpiredTokensTests(UsersApiFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SweepExpiredTokens_RemovesExpiredAndStaleAuthRecords()
    {
        await using var arrangeScope = fixture.Services.CreateAsyncScope();
        var db = arrangeScope.ServiceProvider.GetRequiredService<UsersDbContext>();

        var user = User.CreateWithPassword(
            Email.Create("sweep@example.test").Value,
            new PasswordHash("hashed-password"),
            "Sweep Test").Value;

        var oldClock = new FixedClock(DateTimeOffset.UtcNow.AddDays(-10));
        var freshClock = new FixedClock(DateTimeOffset.UtcNow);

        var (expiredRefreshToken, _) = RefreshToken.Issue(
            user.Id,
            TimeSpan.FromDays(1),
            oldClock,
            "integration-test",
            "127.0.0.1");
        var (freshRefreshToken, _) = RefreshToken.Issue(
            user.Id,
            TimeSpan.FromDays(30),
            freshClock,
            "integration-test",
            "127.0.0.1");

        var (expiredSingleUseToken, _) = SingleUseToken.Create(
            user.Id,
            TokenPurpose.PasswordReset,
            TimeSpan.FromDays(1),
            oldClock);
        var (freshSingleUseToken, _) = SingleUseToken.Create(
            user.Id,
            TokenPurpose.PasswordReset,
            TimeSpan.FromDays(30),
            freshClock);

        var pendingEmail = PendingEmailChange.Create(
            user.Id,
            Email.Create("new@example.test").Value,
            expiredSingleUseToken.Id);

        var (expiredPendingExternalLogin, _) = PendingExternalLogin.Create(
            ExternalLoginProvider.Google,
            "expired-subject",
            "expired@example.test",
            "Expired",
            null,
            isExistingUser: false,
            createdFromIp: null,
            userAgent: null,
            TimeSpan.FromDays(1),
            oldClock);
        var (freshPendingExternalLogin, _) = PendingExternalLogin.Create(
            ExternalLoginProvider.Google,
            "fresh-subject",
            "fresh@example.test",
            "Fresh",
            null,
            isExistingUser: false,
            createdFromIp: null,
            userAgent: null,
            TimeSpan.FromDays(30),
            freshClock);

        db.Users.Add(user);
        db.RefreshTokens.AddRange(expiredRefreshToken, freshRefreshToken);
        db.SingleUseTokens.AddRange(expiredSingleUseToken, freshSingleUseToken);
        db.PendingEmailChanges.Add(pendingEmail);
        db.PendingExternalLogins.AddRange(expiredPendingExternalLogin, freshPendingExternalLogin);
        await db.SaveChangesAsync();

        var bus = fixture.ApplicationHost.Services.GetRequiredService<IMessageBus>();
        await bus.InvokeAsync(new SweepExpiredTokens(), CancellationToken.None);

        await using var assertScope = fixture.Services.CreateAsyncScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<UsersDbContext>();

        Assert.DoesNotContain(await assertDb.RefreshTokens.ToListAsync(), t => t.Id == expiredRefreshToken.Id);
        Assert.Contains(await assertDb.RefreshTokens.ToListAsync(), t => t.Id == freshRefreshToken.Id);
        Assert.DoesNotContain(await assertDb.SingleUseTokens.ToListAsync(), t => t.Id == expiredSingleUseToken.Id);
        Assert.Contains(await assertDb.SingleUseTokens.ToListAsync(), t => t.Id == freshSingleUseToken.Id);
        Assert.Empty(await assertDb.PendingEmailChanges.ToListAsync());
        Assert.DoesNotContain(await assertDb.PendingExternalLogins.ToListAsync(), p => p.Id == expiredPendingExternalLogin.Id);
        Assert.Contains(await assertDb.PendingExternalLogins.ToListAsync(), p => p.Id == freshPendingExternalLogin.Id);
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
