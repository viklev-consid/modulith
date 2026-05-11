namespace Modulith.Modules.Users.Features.RevokeInvitation;

public sealed record RevokeInvitationResponse(Guid InvitationId, string Email, DateTimeOffset RevokedAt);
