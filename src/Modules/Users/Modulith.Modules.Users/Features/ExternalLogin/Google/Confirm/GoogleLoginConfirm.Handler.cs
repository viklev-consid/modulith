using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Modulith.Modules.Users.Contracts;
using Modulith.Modules.Users.Contracts.Events;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Errors;
using Modulith.Modules.Users.Persistence;
using Modulith.Modules.Users.Security;
using Modulith.Shared.Kernel.Interfaces;
using Npgsql;
using Wolverine;

namespace Modulith.Modules.Users.Features.ExternalLogin.Google.Confirm;

public sealed class GoogleLoginConfirmHandler(
    UsersDbContext db,
    IJwtGenerator jwtGenerator,
    IRefreshTokenIssuer refreshTokenIssuer,
    IOptions<UsersOptions> options,
    IMessageBus bus,
    IClock clock)
{
    public async Task<ErrorOr<GoogleLoginConfirmResponse>> Handle(GoogleLoginConfirmCommand cmd, CancellationToken ct)
        => await UsersTelemetry.InstrumentAsync(nameof(GoogleLoginConfirmHandler), () => HandleCoreAsync(cmd, ct));

    private async Task<ErrorOr<GoogleLoginConfirmResponse>> HandleCoreAsync(GoogleLoginConfirmCommand cmd, CancellationToken ct)
    {
        var tokenHash = PendingExternalLogin.HashRawValue(cmd.Token);
        var pending = await db.PendingExternalLogins
            .FirstOrDefaultAsync(p => p.TokenHash == tokenHash, ct);

        if (pending is null)
        {
            return UsersErrors.InvalidOrExpiredToken;
        }

        var consumeResult = pending.Consume(clock);
        if (consumeResult.IsError)
        {
            return consumeResult.Errors;
        }

        var opts = options.Value;
        var now = clock.UtcNow;

        if (pending.IsExistingUser)
        {
            return await ConfirmExistingUserAsync(pending, cmd, opts, now, ct);
        }

        return await ProvisionNewUserAsync(pending, cmd, opts, now, ct);
    }

    private async Task<ErrorOr<GoogleLoginConfirmResponse>> ConfirmExistingUserAsync(
        PendingExternalLogin pending,
        GoogleLoginConfirmCommand cmd,
        UsersOptions opts,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var emailResult = Email.Create(pending.Email);
        if (emailResult.IsError)
        {
            return UsersErrors.ExternalAuthUnavailable;
        }

        var user = await db.Users
            .Include(u => u.ExternalLogins)
            .FirstOrDefaultAsync(u => u.Email == emailResult.Value, ct);

        if (user is null)
        {
            return UsersErrors.UserNotFound;
        }

        var linkResult = user.LinkExternalLogin(pending.Provider, pending.Subject, now);
        if (linkResult.IsError)
        {
            return linkResult.Errors;
        }

        var (refreshToken, rawRefreshToken) = await refreshTokenIssuer.IssueAsync(user.Id, ct);
        await db.SaveChangesAsync(ct);

        await bus.PublishAsync(new ExternalLoginLinkedV1(user.Id.Value, pending.Provider.ToString(), pending.Subject, now, Guid.NewGuid()));
        await bus.PublishAsync(new UserLoggedInV1(user.Id.Value, user.Email.Value, cmd.IpAddress ?? string.Empty));
        UsersTelemetry.EventsPublished.Add(2, new KeyValuePair<string, object?>("event", "ExternalLoginLinkedV1+UserLoggedInV1"));

        var accessToken = jwtGenerator.Generate(user.Id, user.Email.Value, user.DisplayName, user.Role.Name, refreshToken.Id.Value);

        return new GoogleLoginConfirmResponse(
            user.Id.Value,
            accessToken,
            now.AddMinutes(opts.AccessTokenLifetimeMinutes),
            rawRefreshToken,
            refreshToken.ExpiresAt,
            IsNewUser: false);
    }

    private async Task<ErrorOr<GoogleLoginConfirmResponse>> ProvisionNewUserAsync(
        PendingExternalLogin pending,
        GoogleLoginConfirmCommand cmd,
        UsersOptions opts,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var emailResult = Email.Create(pending.Email);
        if (emailResult.IsError)
        {
            return UsersErrors.ExternalAuthUnavailable;
        }

        var userResult = User.CreateExternal(emailResult.Value, pending.DisplayName, pending.Provider, pending.Subject, clock);
        if (userResult.IsError)
        {
            return userResult.Errors;
        }

        var user = userResult.Value;

        var linkResult = user.LinkExternalLogin(pending.Provider, pending.Subject, now);
        if (linkResult.IsError)
        {
            return linkResult.Errors;
        }

        db.Users.Add(user);
        db.Consents.Add(Consent.Grant(user.Id.Value, ConsentKeys.WelcomeEmail, now, cmd.IpAddress, cmd.UserAgent));

        var (refreshToken, rawRefreshToken) = await refreshTokenIssuer.IssueAsync(user.Id, ct);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            return UsersErrors.EmailAlreadyRegistered;
        }

        await bus.PublishAsync(new UserProvisionedFromExternalV1(
            user.Id.Value, pending.Provider.ToString(), pending.Subject,
            user.Email.Value, user.DisplayName, now, Guid.NewGuid()));
        UsersTelemetry.EventsPublished.Add(1, new KeyValuePair<string, object?>("event", nameof(UserProvisionedFromExternalV1)));

        var accessToken = jwtGenerator.Generate(user.Id, user.Email.Value, user.DisplayName, user.Role.Name, refreshToken.Id.Value);

        return new GoogleLoginConfirmResponse(
            user.Id.Value,
            accessToken,
            now.AddMinutes(opts.AccessTokenLifetimeMinutes),
            rawRefreshToken,
            refreshToken.ExpiresAt,
            IsNewUser: true);
    }
}
