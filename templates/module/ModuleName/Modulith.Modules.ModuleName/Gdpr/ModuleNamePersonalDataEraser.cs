using Modulith.Shared.Kernel.Gdpr;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.ModuleName.Gdpr;

public sealed class ModuleNamePersonalDataEraser : IPersonalDataEraser
{
    public Task<ErasureResult> EraseAsync(UserRef user, ErasureStrategy strategy, CancellationToken ct)
    {
        return Task.FromResult(new ErasureResult(user.UserId, strategy, RecordsAffected: 0));
    }
}
