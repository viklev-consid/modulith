using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Modulith.Modules.Users.Contracts;
using Modulith.Modules.Users.Contracts.Events;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Errors;
using Modulith.Modules.Users.Persistence;
using Modulith.Modules.Users.Security;
using Modulith.Shared.Infrastructure.Persistence;
using Modulith.Shared.Kernel.Interfaces;
using Wolverine;

namespace Modulith.Modules.Users.Features.Register;

public sealed class RegisterHandler(
    UsersDbContext db,
    IPasswordHasher passwordHasher,
    IJwtGenerator jwtGenerator,
    IRefreshTokenIssuer refreshTokenIssuer,
    IOptions<UsersOptions> options,
    IMessageBus bus,
    IClock clock)
{
    public async Task<ErrorOr<RegisterResponse>> Handle(RegisterCommand cmd, CancellationToken ct)
        => await UsersTelemetry.InstrumentAsync(nameof(RegisterHandler), () => HandleCoreAsync(cmd, ct));

    private async Task<ErrorOr<RegisterResponse>> HandleCoreAsync(RegisterCommand cmd, CancellationToken ct)
    {
        var emailResult = Email.Create(cmd.Email);
        if (emailResult.IsError)
        {
            return emailResult.Errors;
        }

        var email = emailResult.Value;

        if (await db.Users.AnyAsync(u => u.Email == email, ct))
        {
            return UsersErrors.EmailAlreadyRegistered;
        }

        var passwordHash = new PasswordHash(passwordHasher.Hash(cmd.Password));
        var userResult = User.CreateWithPassword(email, passwordHash, cmd.DisplayName);
        if (userResult.IsError)
        {
            return userResult.Errors;
        }

        var user = userResult.Value;
        db.Users.Add(user);

        // Grant default consents on registration.
        db.Consents.Add(Consent.Grant(user.Id.Value, ConsentKeys.WelcomeEmail, clock.UtcNow));

        // Issue refresh token alongside initial access token.
        var (refreshToken, rawRefreshToken) = await refreshTokenIssuer.IssueAsync(user.Id, ct);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation())
        {
            // A concurrent registration claimed the same email between our pre-check and commit.
            return UsersErrors.EmailAlreadyRegistered;
        }

        await bus.PublishAsync(new UserRegisteredV1(user.Id.Value, user.Email.Value, user.DisplayName, Guid.NewGuid()));
        UsersTelemetry.EventsPublished.Add(1, new KeyValuePair<string, object?>("event", nameof(UserRegisteredV1)));

        var accessTokenExpiresAt = clock.UtcNow.AddMinutes(options.Value.AccessTokenLifetimeMinutes);
        var accessToken = jwtGenerator.Generate(user.Id, user.Email.Value, user.DisplayName, user.Role.Name, refreshToken.Id.Value);

        return new RegisterResponse(
            user.Id.Value,
            accessToken,
            accessTokenExpiresAt,
            rawRefreshToken,
            refreshToken.ExpiresAt);
    }
}
