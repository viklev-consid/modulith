using Modulith.Shared.Kernel.Gdpr;

namespace Modulith.Modules.Organizations.Contracts.Events;

public sealed record OrganizationInvitationCreatedV1(
    Guid OrganizationId,
    Guid InvitationId,
    [property: PersonalData] string Email,
    string Role,
    [property: SensitivePersonalData] string RawToken,
    Guid InvitedByUserId,
    Guid EventId);
