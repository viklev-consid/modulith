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

namespace Modulith.Modules.Users.Features.Login;

public sealed class LoginHandler(
    UsersDbContext db,
    IPasswordHasher passwordHasher,
    IJwtGenerator jwtGenerator,
    IRefreshTokenIssuer refreshTokenIssuer,
    IOptions<UsersOptions> options,
    IMessageBus bus,
    IClock clock)
{
    public async Task<ErrorOr<LoginResponse>> Handle(LoginCommand cmd, CancellationToken ct)
    {
        var emailResult = Email.Create(cmd.Email);
        if (emailResult.IsError)
            return UsersErrors.InvalidCredentials;

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == emailResult.Value, ct);
        if (user is null)
            return UsersErrors.InvalidCredentials;

        if (!passwordHasher.Verify(cmd.Password, user.PasswordHash.Value))
            return UsersErrors.InvalidCredentials;

        var (refreshToken, rawRefreshToken) = await refreshTokenIssuer.IssueAsync(user.Id, ct);

        await db.SaveChangesAsync(ct);

        await bus.PublishAsync(new UserLoggedInV1(
            user.Id.Value,
            user.Email.Value,
            cmd.IpAddress ?? string.Empty));

        var accessTokenExpiresAt = clock.UtcNow.AddMinutes(options.Value.AccessTokenLifetimeMinutes);
        var accessToken = jwtGenerator.Generate(user.Id, user.Email.Value, user.DisplayName, refreshToken.Id.Value);

        return new LoginResponse(
            user.Id.Value,
            accessToken,
            accessTokenExpiresAt,
            rawRefreshToken,
            refreshToken.ExpiresAt);
    }
}
