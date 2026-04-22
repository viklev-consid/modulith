using ErrorOr;
using Modulith.Modules.Users.Errors;
using Modulith.Modules.Users.Gdpr;
using Modulith.Modules.Users.Persistence;
using Modulith.Shared.Kernel.Gdpr;

namespace Modulith.Modules.Users.Features.ExportPersonalData;

public sealed class ExportPersonalDataHandler(
    UsersDbContext db,
    PersonalDataOrchestrator orchestrator)
{
    public async Task<ErrorOr<ExportPersonalDataResponse>> Handle(
        ExportPersonalDataQuery query, CancellationToken ct)
        => await UsersTelemetry.InstrumentAsync(nameof(ExportPersonalDataHandler), () => HandleCoreAsync(query, ct));

    private async Task<ErrorOr<ExportPersonalDataResponse>> HandleCoreAsync(
        ExportPersonalDataQuery query, CancellationToken ct)
    {
        var user = await db.Users.FindAsync([query.UserId], ct);
        if (user is null)
        {
            return UsersErrors.UserNotFound;
        }

        var userRef = new UserRef(user.Id.Value, user.DisplayName);

        var exports = new List<PersonalDataExport>();
        foreach (var exporter in orchestrator.Exporters)
        {
            exports.Add(await exporter.ExportAsync(userRef, ct));
        }

        return new ExportPersonalDataResponse(exports);
    }
}
