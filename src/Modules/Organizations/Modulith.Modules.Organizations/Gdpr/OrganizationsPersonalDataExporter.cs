using Modulith.Shared.Kernel.Gdpr;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Organizations.Gdpr;

public sealed class OrganizationsPersonalDataExporter : IPersonalDataExporter
{
    public Task<PersonalDataExport> ExportAsync(UserRef user, CancellationToken ct)
    {
        var data = new Dictionary<string, object?>(StringComparer.Ordinal);
        return Task.FromResult(new PersonalDataExport(user.UserId, "Organizations", data));
    }
}
