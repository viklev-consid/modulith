using Modulith.Shared.Kernel.Gdpr;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Organizations.Gdpr;

public sealed class OrganizationsPersonalDataEraser : IPersonalDataEraser
{
    public Task<ErasureResult> EraseAsync(UserRef user, ErasureStrategy strategy, CancellationToken ct)
    {
        return Task.FromResult(new ErasureResult(user.UserId, strategy, RecordsAffected: 0));
    }
}
