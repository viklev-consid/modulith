using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Persistence;

namespace Modulith.Modules.Users.Security;

internal sealed class TwoFactorRequirementEvaluator(UsersDbContext db) : ITwoFactorRequirementEvaluator
{
    public async Task<bool> IsRequiredAsync(User user, CancellationToken ct) =>
        await db.TwoFactorCredentials.AnyAsync(c =>
            c.UserId == user.Id &&
            c.Method == TwoFactorMethod.Totp &&
            c.ConfirmedAt != null &&
            c.DisabledAt == null,
            ct);
}
