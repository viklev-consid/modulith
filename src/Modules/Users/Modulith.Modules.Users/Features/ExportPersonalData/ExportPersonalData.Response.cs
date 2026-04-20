using Modulith.Shared.Kernel.Gdpr;

namespace Modulith.Modules.Users.Features.ExportPersonalData;

public sealed record ExportPersonalDataResponse(IReadOnlyList<PersonalDataExport> Exports);
