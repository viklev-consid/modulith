using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Modulith.Modules.Users.Contracts.Events;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Errors;
using Modulith.Modules.Users.Persistence;
using Modulith.Modules.Users.Security;
using Modulith.Shared.Kernel.Interfaces;
using Wolverine;

namespace Modulith.Modules.Users.Features.LoginTwoFactor;

public sealed class LoginTwoFactorHandler(
    UsersDbContext db,
    ITotpService totpService,
    ITotpSecretProtector secretProtector,
    IJwtGenerator jwtGenerator,
    IRefreshTokenIssuer refreshTokenIssuer,
    IOptions<UsersOptions> options,
    IMessageBus bus,
    IClock clock)
{
    public async Task<ErrorOr<LoginTwoFactorResponse>> Handle(LoginTwoFactorCommand cmd, CancellationToken ct)
        => await UsersTelemetry.InstrumentAsync(nameof(LoginTwoFactorHandler), () => HandleCoreAsync(cmd, ct));

    private async Task<ErrorOr<LoginTwoFactorResponse>> HandleCoreAsync(LoginTwoFactorCommand cmd, CancellationToken ct)
    {
        var challengeHash = PendingTwoFactorChallenge.HashRawValue(cmd.ChallengeToken);
        var challenge = await db.PendingTwoFactorChallenges
            .FirstOrDefaultAsync(c => c.TokenHash == challengeHash, ct);

        if (challenge is null || !challenge.IsValid(clock))
        {
            return UsersErrors.InvalidOrExpiredToken;
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == challenge.UserId, ct);
        if (user is null)
        {
            return UsersErrors.InvalidOrExpiredToken;
        }

        var credential = await db.TwoFactorCredentials
            .FirstOrDefaultAsync(c =>
                c.UserId == user.Id &&
                c.Method == TwoFactorMethod.Totp &&
                c.ConfirmedAt != null &&
                c.DisabledAt == null,
                ct);

        if (credential is null)
        {
            return UsersErrors.TwoFactorNotEnabled;
        }

        var verifyResult = await VerifyCodeAsync(user.Id, credential, cmd.Code, ct);
        if (verifyResult.IsError)
        {
            _ = challenge.RecordFailedAttempt(clock);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                return UsersErrors.TwoFactorCodeInvalid;
            }

            return verifyResult.Errors;
        }

        var consumeResult = challenge.Consume(clock);
        if (consumeResult.IsError)
        {
            return consumeResult.Errors;
        }

        var (refreshToken, rawRefreshToken) = await refreshTokenIssuer.IssueAsync(user.Id, ct);
        await db.SaveChangesAsync(ct);

        await bus.PublishAsync(new UserLoggedInV1(
            user.Id.Value,
            user.Email.Value,
            cmd.IpAddress ?? string.Empty,
            Guid.NewGuid()));
        UsersTelemetry.EventsPublished.Add(1, new KeyValuePair<string, object?>("event", nameof(UserLoggedInV1)));

        var accessTokenExpiresAt = clock.UtcNow.AddMinutes(options.Value.AccessTokenLifetimeMinutes);
        var accessToken = jwtGenerator.Generate(user.Id, user.Email.Value, user.DisplayName, user.Role.Name, refreshToken.Id.Value);

        return new LoginTwoFactorResponse(
            user.Id.Value,
            accessToken,
            accessTokenExpiresAt,
            rawRefreshToken,
            refreshToken.ExpiresAt);
    }

    private async Task<ErrorOr<Success>> VerifyCodeAsync(
        UserId userId,
        TwoFactorCredential credential,
        string code,
        CancellationToken ct)
    {
        if (IsRecoveryCode(code))
        {
            var hash = RecoveryCode.HashRawValue(NormalizeRecoveryCode(code));
            var recoveryCode = await db.RecoveryCodes.FirstOrDefaultAsync(c =>
                c.UserId == userId &&
                c.CodeHash == hash &&
                c.ConsumedAt == null,
                ct);

            if (recoveryCode is null)
            {
                return UsersErrors.RecoveryCodeInvalid;
            }

            return recoveryCode.Consume(clock);
        }

        var verification = totpService.Verify(
            secretProtector.Unprotect(credential.ProtectedSecret),
            code,
            options.Value.TotpAllowedTimeStepDrift);

        if (!verification.IsValid)
        {
            return UsersErrors.TwoFactorCodeInvalid;
        }

        return credential.MarkAcceptedTimeStep(verification.TimeStep);
    }

    private static bool IsRecoveryCode(string code)
    {
        var parts = code.Split('-', StringSplitOptions.None);
        return parts.Length == 4
            && parts.All(p => p.Length == 5 && p.All(Uri.IsHexDigit));
    }

    private static string NormalizeRecoveryCode(string code) =>
        code.ToLowerInvariant();
}
