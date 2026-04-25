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
        var userId = new UserId(user.UserId);
        var dbUser = await db.Users
            .Include(u => u.ExternalLogins)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (dbUser is null)
        {
            return new PersonalDataExport(user.UserId, "Users", new Dictionary<string, object?>(StringComparer.Ordinal));
        }

        var consents = await db.Consents
            .Where(c => c.UserId == user.UserId)
            .Select(c => new { c.ConsentKey, c.Granted, c.RecordedAt })
            .ToListAsync(ct);

        var termsAcceptances = await db.TermsAcceptances
            .Where(t => t.UserId == userId)
            .Select(t => new { t.Version, t.AcceptedAt })
            .ToListAsync(ct);

        var data = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["email"] = dbUser.Email.Value,
            ["displayName"] = dbUser.DisplayName,
            ["role"] = dbUser.Role.Name,
            ["hasPassword"] = dbUser.PasswordHash is not null,
            ["hasCompletedOnboarding"] = dbUser.HasCompletedOnboarding,
            ["linkedProviders"] = dbUser.ExternalLogins.Select(e => e.Provider.ToString()).ToList(),
            ["createdAt"] = dbUser.CreatedAt,
            ["updatedAt"] = dbUser.UpdatedAt,
            ["consents"] = consents,
            ["termsAcceptances"] = termsAcceptances,
        };

        return new PersonalDataExport(user.UserId, "Users", data);
    }
}
