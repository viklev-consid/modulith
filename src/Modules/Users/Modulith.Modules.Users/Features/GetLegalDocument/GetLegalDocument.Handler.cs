using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Users.Errors;
using Modulith.Modules.Users.Legal;
using Modulith.Modules.Users.Persistence;

namespace Modulith.Modules.Users.Features.GetLegalDocument;

public sealed class GetLegalDocumentHandler(UsersDbContext db)
{
    public async Task<ErrorOr<GetLegalDocumentResponse>> Handle(GetLegalDocumentQuery query, CancellationToken ct)
        => await UsersTelemetry.InstrumentAsync(nameof(GetLegalDocumentHandler), () => HandleCoreAsync(query, ct));

    private async Task<ErrorOr<GetLegalDocumentResponse>> HandleCoreAsync(GetLegalDocumentQuery query, CancellationToken ct)
    {
        var document = await db.LegalDocuments
            .AsNoTracking()
            .Where(d => d.DocumentType == query.DocumentType)
            .Where(d => d.Version == query.Version)
            .Where(d => d.SupersededAt == null)
            .Select(d => new GetLegalDocumentResponse(
                d.Id.Value,
                LegalDocumentMapper.ToWireType(d.DocumentType),
                d.Title,
                d.Version,
                d.EffectiveAt,
                d.PublishedAt,
                d.ContentHash,
                d.MarkdownContent))
            .FirstOrDefaultAsync(ct);

        return document is null
            ? UsersErrors.LegalDocumentNotFound
            : document;
    }
}
