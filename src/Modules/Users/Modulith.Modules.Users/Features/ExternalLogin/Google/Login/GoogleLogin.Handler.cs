using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Modulith.Modules.Users.Contracts.Events;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Persistence;
using Modulith.Modules.Users.Errors;
using Modulith.Modules.Users.Security;
using Modulith.Shared.Kernel.Interfaces;
using Wolverine;

namespace Modulith.Modules.Users.Features.ExternalLogin.Google.Login;

public sealed class GoogleLoginHandler(
    UsersDbContext db,
    IGoogleIdTokenVerifier verifier,
    IJwtGenerator jwtGenerator,
    IRefreshTokenIssuer refreshTokenIssuer,
    IOptions<UsersOptions> options,
    IMessageBus bus,
    IClock clock)
{
    public async Task<ErrorOr<GoogleLoginResponse>> Handle(GoogleLoginCommand cmd, CancellationToken ct)
        => await UsersTelemetry.InstrumentAsync(nameof(GoogleLoginHandler), () => HandleCoreAsync(cmd, ct));

    private async Task<ErrorOr<GoogleLoginResponse>> HandleCoreAsync(GoogleLoginCommand cmd, CancellationToken ct)
    {
        var identityResult = await verifier.VerifyAsync(cmd.IdToken, ct);
        if (identityResult.IsError)
        {
            return identityResult.Errors;
        }

        var identity = identityResult.Value;
        var opts = options.Value;

        // Fast path: (provider, subject) already linked — issue tokens immediately.
        var existingLogin = await db.ExternalLogins
            .FirstOrDefaultAsync(e => e.Provider == ExternalLoginProvider.Google && e.Subject == identity.Subject, ct);

        if (existingLogin is not null)
        {
            var user = await db.Users
                .Include(u => u.ExternalLogins)
                .FirstOrDefaultAsync(u => u.Id == existingLogin.UserId, ct);

            if (user is null)
            {
                return UsersErrors.UserNotFound;
            }

            var (refreshToken, rawRefreshToken) = await refreshTokenIssuer.IssueAsync(user.Id, ct);
            await db.SaveChangesAsync(ct);

            await bus.PublishAsync(new UserLoggedInV1(user.Id.Value, user.Email.Value, cmd.IpAddress ?? string.Empty));
            UsersTelemetry.EventsPublished.Add(1, new KeyValuePair<string, object?>("event", nameof(UserLoggedInV1)));

            var expiresAt = clock.UtcNow.AddMinutes(opts.AccessTokenLifetimeMinutes);
            var accessToken = jwtGenerator.Generate(user.Id, user.Email.Value, user.DisplayName, user.Role.Name, refreshToken.Id.Value);

            return new GoogleLoginResponse(
                IsPending: false,
                UserId: user.Id.Value,
                AccessToken: accessToken,
                AccessTokenExpiresAt: expiresAt,
                RefreshToken: rawRefreshToken,
                RefreshTokenExpiresAt: refreshToken.ExpiresAt);
        }

        // Uniform email-loop: all other cases create a pending record and return 202.
        var emailResult = Email.Create(identity.Email);
        if (emailResult.IsError)
        {
            // Malformed email from a valid ID token — treat as unavailable, not user error.
            return UsersErrors.ExternalAuthUnavailable;
        }

        var isExistingUser = await db.Users.AnyAsync(u => u.Email == emailResult.Value, ct);

        var activePendingCount = await db.PendingExternalLogins
            .CountAsync(p => p.Provider == ExternalLoginProvider.Google
                && p.Subject == identity.Subject
                && p.ConsumedAt == null
                && p.ExpiresAt > clock.UtcNow, ct);

        if (activePendingCount < opts.MaxPendingExternalLoginsPerEmail)
        {
            var (pending, rawToken) = PendingExternalLogin.Create(
                ExternalLoginProvider.Google,
                identity.Subject,
                identity.Email,
                identity.Name,
                isExistingUser,
                cmd.IpAddress,
                cmd.UserAgent,
                opts.PendingExternalLoginLifetime,
                clock);

            db.PendingExternalLogins.Add(pending);
            await db.SaveChangesAsync(ct);

            await bus.PublishAsync(new ExternalLoginPendingV1(
                "Google", identity.Email, identity.Name, isExistingUser, rawToken, Guid.NewGuid()));
            UsersTelemetry.EventsPublished.Add(1, new KeyValuePair<string, object?>("event", nameof(ExternalLoginPendingV1)));
        }

        return new GoogleLoginResponse(IsPending: true);
    }
}
