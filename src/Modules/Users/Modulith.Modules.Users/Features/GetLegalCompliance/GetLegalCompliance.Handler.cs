using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Legal;
using Modulith.Modules.Users.Persistence;

namespace Modulith.Modules.Users.Features.GetLegalCompliance;

public sealed class GetLegalComplianceHandler(UsersDbContext db, ILegalComplianceService complianceService)
{
    public async Task<ErrorOr<GetLegalComplianceResponse>> Handle(GetLegalComplianceQuery query, CancellationToken ct)
        => await UsersTelemetry.InstrumentAsync(nameof(GetLegalComplianceHandler), () => HandleCoreAsync(query, ct));

    private async Task<ErrorOr<GetLegalComplianceResponse>> HandleCoreAsync(GetLegalComplianceQuery query, CancellationToken ct)
    {
        var compliance = await complianceService.GetContinuedUseComplianceAsync(query.UserId, ct);
        var acceptedDocuments = await db.TermsAcceptances
            .AsNoTracking()
            .Where(a => a.UserId == query.UserId)
            .OrderByDescending(a => a.AcceptedAt)
            .Take(50)
            .Select(a => new AcceptedLegalDocumentResponse(
                a.DocumentType == null ? "termsOfService" : LegalDocumentMapper.ToWireType(a.DocumentType.Value),
                a.Version,
                a.AcceptedAt,
                a.ContentHash))
            .ToListAsync(ct);

        return new GetLegalComplianceResponse(
            compliance.IsCompliant,
            LegalDocumentMapper.ToWireBlockingLevel(compliance.BlockingLevel),
            compliance.MissingDocuments.Select(ToResponse).ToList(),
            acceptedDocuments);
    }

    private static LegalComplianceDocumentResponse ToResponse(LegalDocument document) =>
        new(
            document.Id.Value,
            LegalDocumentMapper.ToWireType(document.DocumentType),
            document.Title,
            document.Version,
            document.EffectiveAt,
            document.ContentHash,
            document.MarkdownContent);
}
