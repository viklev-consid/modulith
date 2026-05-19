using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Users.Legal;
using Modulith.Modules.Users.Persistence;

namespace Modulith.Modules.Users.Features.GetOnboardingLegalRequirements;

public sealed class GetOnboardingLegalRequirementsHandler(UsersDbContext db)
{
    public async Task<ErrorOr<GetOnboardingLegalRequirementsResponse>> Handle(
        GetOnboardingLegalRequirementsQuery query,
        CancellationToken ct)
        => await UsersTelemetry.InstrumentAsync(nameof(GetOnboardingLegalRequirementsHandler), () => HandleCoreAsync(ct));

    private async Task<ErrorOr<GetOnboardingLegalRequirementsResponse>> HandleCoreAsync(CancellationToken ct)
    {
        var documents = await db.LegalDocuments
            .AsNoTracking()
            .Where(d => d.IsRequiredForOnboarding && d.SupersededAt == null)
            .OrderBy(d => d.DocumentType)
            .Select(d => new OnboardingLegalDocumentResponse(
                d.Id.Value,
                LegalDocumentMapper.ToWireType(d.DocumentType),
                d.Title,
                d.Version,
                d.EffectiveAt,
                d.ContentHash,
                d.MarkdownContent))
            .ToListAsync(ct);

        return new GetOnboardingLegalRequirementsResponse(documents);
    }
}
