using Modulith.Modules.Organizations.Contracts.Commands;

namespace Modulith.Modules.Users.Features.DeleteAccount;

public sealed record DeleteAccountResponse(IReadOnlyCollection<UserErasureBlockingOrganization> BlockingOrganizations)
{
    public bool CanDelete => BlockingOrganizations.Count == 0;
}
