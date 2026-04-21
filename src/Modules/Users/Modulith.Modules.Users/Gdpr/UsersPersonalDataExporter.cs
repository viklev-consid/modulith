using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Persistence;
using Modulith.Shared.Kernel.Gdpr;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Users.Gdpr;

public sealed class UsersPersonalDataExporter(UsersDbContext db) : IPersonalDataExporter
{
    public async Task<PersonalDataExport> ExportAsync(UserRef user, CancellationToken ct)
    {
        var dbUser = await db.Users.FindAsync([new UserId(user.UserId)], ct);
        if (dbUser is null)
        {
            return new PersonalDataExport(user.UserId, "Users", new Dictionary<string, object?>());
        }

        var data = new Dictionary<string, object?>
        {
            ["email"] = dbUser.Email.Value,
            ["displayName"] = dbUser.DisplayName,
            ["createdAt"] = dbUser.CreatedAt,
            ["updatedAt"] = dbUser.UpdatedAt,
        };

        return new PersonalDataExport(user.UserId, "Users", data);
    }
}
