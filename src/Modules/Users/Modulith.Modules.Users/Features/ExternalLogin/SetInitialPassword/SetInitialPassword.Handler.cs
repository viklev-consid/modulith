using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Errors;
using Modulith.Modules.Users.Persistence;
using Modulith.Modules.Users.Security;

namespace Modulith.Modules.Users.Features.ExternalLogin.SetInitialPassword;

public sealed class SetInitialPasswordHandler(UsersDbContext db, IPasswordHasher passwordHasher)
{
    public async Task<ErrorOr<Success>> Handle(SetInitialPasswordCommand cmd, CancellationToken ct)
        => await UsersTelemetry.InstrumentAsync(nameof(SetInitialPasswordHandler), () => HandleCoreAsync(cmd, ct));

    private async Task<ErrorOr<Success>> HandleCoreAsync(SetInitialPasswordCommand cmd, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == new UserId(cmd.UserId), ct);

        if (user is null)
        {
            return UsersErrors.UserNotFound;
        }

        var hash = new PasswordHash(passwordHasher.Hash(cmd.Password));
        var result = user.SetInitialPassword(hash);
        if (result.IsError)
        {
            return result.Errors;
        }

        await db.SaveChangesAsync(ct);
        return Result.Success;
    }
}
