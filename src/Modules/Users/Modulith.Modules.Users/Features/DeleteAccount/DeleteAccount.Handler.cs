using ErrorOr;
using Modulith.Modules.Users.Contracts.Events;
using Modulith.Modules.Users.Errors;
using Modulith.Modules.Users.Gdpr;
using Modulith.Modules.Users.Persistence;
using Modulith.Shared.Kernel.Gdpr;
using Wolverine;

namespace Modulith.Modules.Users.Features.DeleteAccount;

public sealed class DeleteAccountHandler(
    UsersDbContext db,
    UsersPersonalDataEraser eraser,
    IMessageBus bus)
{
    public async Task<ErrorOr<Deleted>> Handle(DeleteAccountCommand cmd, CancellationToken ct)
        => await UsersTelemetry.InstrumentAsync(nameof(DeleteAccountHandler), () => HandleCoreAsync(cmd, ct));

    private async Task<ErrorOr<Deleted>> HandleCoreAsync(DeleteAccountCommand cmd, CancellationToken ct)
    {
        var user = await db.Users.FindAsync([cmd.UserId], ct);
        if (user is null)
        {
            return UsersErrors.UserNotFound;
        }

        var userRef = new UserRef(user.Id.Value, user.DisplayName);

        await eraser.EraseAsync(userRef, ErasureStrategy.HardDelete, ct);

        await bus.PublishAsync(new UserErasureRequestedV1(userRef.UserId, userRef.DisplayName, Guid.NewGuid()));

        return Result.Deleted;
    }
}
