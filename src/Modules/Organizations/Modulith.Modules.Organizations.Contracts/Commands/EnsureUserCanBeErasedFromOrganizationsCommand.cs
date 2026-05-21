using ErrorOr;

namespace Modulith.Modules.Organizations.Contracts.Commands;

public sealed record EnsureUserCanBeErasedFromOrganizationsCommand(Guid UserId);
