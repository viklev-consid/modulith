using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Errors;
using Modulith.Modules.Users.Persistence;
using Modulith.Shared.Infrastructure.Persistence;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Users.Features.AcceptLegalDocuments;

public sealed class AcceptLegalDocumentsHandler(UsersDbContext db, IClock clock)
{
    public async Task<ErrorOr<Success>> Handle(AcceptLegalDocumentsCommand cmd, CancellationToken ct)
        => await UsersTelemetry.InstrumentAsync(nameof(AcceptLegalDocumentsHandler), () => HandleCoreAsync(cmd, ct));

    private async Task<ErrorOr<Success>> HandleCoreAsync(AcceptLegalDocumentsCommand cmd, CancellationToken ct)
    {
        var exists = await db.Users.AnyAsync(u => u.Id == cmd.UserId, ct);
        if (!exists)
        {
            return UsersErrors.UserNotFound;
        }

        var acceptedById = cmd.AcceptedDocuments
            .GroupBy(d => d.DocumentId)
            .ToDictionary(g => g.Key, g => g.First());

        var documents = (await db.LegalDocuments.ToListAsync(ct))
            .Where(d => acceptedById.ContainsKey(d.Id.Value))
            .ToList();

        if (documents.Count != acceptedById.Count)
        {
            return UsersErrors.LegalDocumentAcceptanceInvalid;
        }

        foreach (var document in documents)
        {
            var accepted = acceptedById[document.Id.Value];
            if (document.SupersededAt is not null ||
                !string.Equals(accepted.Version, document.Version, StringComparison.Ordinal) ||
                !string.Equals(accepted.ContentHash, document.ContentHash, StringComparison.Ordinal))
            {
                return UsersErrors.LegalDocumentAcceptanceInvalid;
            }
        }

        var now = clock.UtcNow;
        foreach (var document in documents)
        {
            var versionKey = $"{LegalDocumentKeys.GetPrefix(document.DocumentType)}:{document.Version}";
            var alreadyAccepted = await db.TermsAcceptances.AnyAsync(
                a => a.UserId == cmd.UserId && a.Version == versionKey,
                ct);

            if (alreadyAccepted)
            {
                continue;
            }

            db.TermsAcceptances.Add(TermsAcceptance.Record(cmd.UserId, document, now, cmd.IpAddress, cmd.UserAgent));
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation())
        {
            db.ChangeTracker.Clear();
        }

        return Result.Success;
    }
}
