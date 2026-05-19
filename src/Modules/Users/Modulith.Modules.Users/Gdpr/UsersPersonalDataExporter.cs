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
        var dbUser = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (dbUser is null)
        {
            return new PersonalDataExport(user.UserId, "Users", new Dictionary<string, object?>(StringComparer.Ordinal));
        }

        var consents = await db.Consents
            .Where(c => c.UserId == user.UserId)
            .Select(c => new { c.ConsentKey, c.Granted, c.RecordedAt, c.GrantedFromIp, c.GrantedUserAgent, c.PolicyVersion })
            .ToListAsync(ct);

        var termsAcceptances = await db.TermsAcceptances
            .Where(t => t.UserId == userId)
            .Select(t => new { t.Version, t.AcceptedAt, t.AcceptedFromIp, t.UserAgent })
            .ToListAsync(ct);

        var twoFactorCredential = await db.TwoFactorCredentials
            .Where(c => c.UserId == userId)
            .Select(c => new { method = c.Method.ToString(), enabled = c.ConfirmedAt != null && c.DisabledAt == null, c.ConfirmedAt, c.DisabledAt })
            .FirstOrDefaultAsync(ct);

        var activeRecoveryCodeCount = await db.RecoveryCodes
            .CountAsync(c => c.UserId == userId && c.ConsumedAt == null, ct);

        var data = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["email"] = dbUser.Email.Value,
            ["displayName"] = dbUser.DisplayName,
            ["avatar"] = dbUser.HasAvatar
                ? new
                {
                    container = dbUser.AvatarContainer,
                    key = dbUser.AvatarKey,
                    contentType = dbUser.AvatarContentType,
                    sizeBytes = dbUser.AvatarSizeBytes,
                    updatedAt = dbUser.AvatarUpdatedAt,
                }
                : null,
            ["role"] = dbUser.Role.Name,
            ["hasCompletedOnboarding"] = dbUser.HasCompletedOnboarding,
            ["createdAt"] = dbUser.CreatedAt,
            ["updatedAt"] = dbUser.UpdatedAt,
            ["consents"] = consents,
            ["termsAcceptances"] = termsAcceptances,
            ["twoFactor"] = twoFactorCredential,
            ["activeRecoveryCodeCount"] = activeRecoveryCodeCount,
        };

        return new PersonalDataExport(user.UserId, "Users", data);
    }
}
