using Modulith.Modules.Users.Domain;

namespace Modulith.Modules.Users.Legal;

public interface ILegalComplianceService
{
    Task<LegalComplianceResult> GetContinuedUseComplianceAsync(UserId userId, CancellationToken ct);
}
