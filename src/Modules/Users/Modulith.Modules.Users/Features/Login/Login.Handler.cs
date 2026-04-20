using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Errors;
using Modulith.Modules.Users.Persistence;
using Modulith.Modules.Users.Security;

namespace Modulith.Modules.Users.Features.Login;

public sealed class LoginHandler(
    UsersDbContext db,
    IPasswordHasher passwordHasher,
    IJwtGenerator jwtGenerator)
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

        var token = jwtGenerator.Generate(user.Id, user.Email.Value, user.DisplayName);
        return new LoginResponse(user.Id.Value, token);
    }
}
