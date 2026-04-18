using Modulith.Shared.Kernel.Gdpr;

namespace Modulith.Shared.Kernel.Interfaces;

public interface IPersonalDataExporter
{
    Task<PersonalDataExport> ExportAsync(UserRef user, CancellationToken ct);
}
