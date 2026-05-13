namespace Modulith.Modules.Users.Features.ListInvitations;

public sealed record ListInvitationsInvitationDto(
    Guid InvitationId,
    string Email,
    string Status,
    DateTimeOffset InvitedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? AcceptedAt,
    DateTimeOffset? RevokedAt);

public sealed record ListInvitationsResponse(
    IReadOnlyCollection<ListInvitationsInvitationDto> Invitations,
    int Page,
    int PageSize,
    int TotalCount);
