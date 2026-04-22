using Modulith.Modules.Users.Domain;

namespace Modulith.Modules.Users.Features.GetCurrentUser;

// TokenRole carries the role claim already embedded in the bearer token.
// The handler uses it so that /me reflects the same role that authorization uses,
// keeping the two consistent within a single token's lifetime.
// Falls back to the DB value for tokens issued before the role claim was added.
public sealed record GetCurrentUserQuery(UserId UserId, string? TokenRole);
