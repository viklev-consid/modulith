using Modulith.Shared.Kernel.Gdpr;

namespace Modulith.Shared.Kernel.Interfaces;

public interface IPersonalDataEraser
{
    Task<ErasureResult> EraseAsync(UserRef user, ErasureStrategy strategy, CancellationToken ct);
}
