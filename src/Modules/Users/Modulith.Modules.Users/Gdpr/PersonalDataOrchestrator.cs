using Modulith.Shared.Kernel.Gdpr;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Users.Gdpr;

public sealed class PersonalDataOrchestrator(
    IEnumerable<IPersonalDataExporter> exporters,
    IEnumerable<IPersonalDataEraser> erasers)
{
    public IEnumerable<IPersonalDataExporter> Exporters => exporters;
    public IEnumerable<IPersonalDataEraser> Erasers => erasers;
}
