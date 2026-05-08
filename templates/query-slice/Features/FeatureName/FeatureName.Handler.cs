using ErrorOr;
using Modulith.Modules.ModuleName.Persistence;

namespace Modulith.Modules.ModuleName.Features.FeatureName;

public sealed class FeatureNameHandler(ModuleNameDbContext db)
{
    public async Task<ErrorOr<FeatureNameResponse>> Handle(FeatureNameQuery query, CancellationToken ct)
    {
        _ = (db, query, ct);
        await Task.CompletedTask;
        throw new NotImplementedException();
    }
}
