using Modulith.Shared.Kernel.Gdpr;

namespace Modulith.Modules.Organizations.Features.CreateOrganizationInvitation;

public sealed record CreateOrganizationInvitationResponse(
    Guid InvitationId,
    [property: PersonalData] string Email,
    string Role,
    DateTimeOffset ExpiresAt,
    [property: SensitivePersonalData] string RawToken);
