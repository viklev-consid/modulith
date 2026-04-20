using ErrorOr;
using Modulith.Modules.ModuleName.Persistence;
using Wolverine;

namespace Modulith.Modules.ModuleName.Features.FeatureName;

public sealed class FeatureNameHandler(ModuleNameDbContext db, IMessageBus bus)
{
    public async Task<ErrorOr<FeatureNameResponse>> Handle(FeatureNameCommand cmd, CancellationToken ct)
    {
        _ = (db, bus);
        await Task.CompletedTask;
        throw new NotImplementedException();
    }
}
