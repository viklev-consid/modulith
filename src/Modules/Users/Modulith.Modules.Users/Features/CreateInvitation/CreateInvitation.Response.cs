namespace Modulith.Modules.Users.Features.CreateInvitation;

public sealed record CreateInvitationResponse(Guid InvitationId, string Email, string Token, DateTimeOffset ExpiresAt);
