using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Persistence;

namespace Modulith.Modules.Users.Seeding;

/// <summary>
/// Non-dev hosted service that promotes the first admin on startup.
/// Only runs when <c>Modules:Users:AdminBootstrap:Enabled</c> is <c>true</c>.
///
/// Behaviour:
/// <list type="bullet">
/// <item>If at least one Admin-role user exists → no-op.</item>
/// <item>If a user with the configured email exists → promote to Admin.</item>
/// <item>Otherwise → warn and continue. Does NOT create users from scratch.</item>
/// <item>If Enabled but Email is missing → fail fast at startup.</item>
/// </list>
/// </summary>
internal sealed partial class AdminBootstrapper(
    IServiceProvider services,
    IOptions<AdminBootstrapOptions> opts,
    ILogger<AdminBootstrapper> logger) : IHostedService
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Admin bootstrap: an admin user already exists. No action taken.")]
    private static partial void LogAdminAlreadyExists(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Admin bootstrap: no user found with email {Email}. Register the account first, then restart to promote it.")]
    private static partial void LogUserNotFound(ILogger logger, string email);

    [LoggerMessage(Level = LogLevel.Information, Message = "Admin bootstrap: user {Email} already has the Admin role.")]
    private static partial void LogAlreadyAdmin(ILogger logger, string email);

    [LoggerMessage(Level = LogLevel.Information, Message = "Admin bootstrap: user {Email} promoted to Admin role.")]
    private static partial void LogPromoted(ILogger logger, string email);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var options = opts.Value;
        if (!options.Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(options.Email))
        {
            throw new InvalidOperationException(
                "Modules:Users:AdminBootstrap:Email must be set when AdminBootstrap:Enabled is true.");
        }

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();

        var adminExists = await db.Users
            .AnyAsync(u => u.Role == Role.Admin, cancellationToken);

        if (adminExists)
        {
            LogAdminAlreadyExists(logger);
            return;
        }

        var emailResult = Email.Create(options.Email);
        if (emailResult.IsError)
        {
            throw new InvalidOperationException(
                $"Modules:Users:AdminBootstrap:Email '{options.Email}' is not a valid email address.");
        }

        var user = await db.Users
            .FirstOrDefaultAsync(u => u.Email == emailResult.Value, cancellationToken);

        if (user is null)
        {
            LogUserNotFound(logger, options.Email);
            return;
        }

        var changeResult = user.ChangeRole(Role.Admin, user.Id);
        if (changeResult.IsError)
        {
            LogAlreadyAdmin(logger, options.Email);
            return;
        }

        await db.SaveChangesAsync(cancellationToken);
        LogPromoted(logger, options.Email);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
