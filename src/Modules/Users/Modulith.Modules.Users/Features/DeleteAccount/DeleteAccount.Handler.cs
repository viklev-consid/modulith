using ErrorOr;
using Modulith.Modules.Organizations.Contracts.Commands;
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
    public async Task<ErrorOr<DeleteAccountResponse>> Handle(DeleteAccountCommand cmd, CancellationToken ct)
        => await UsersTelemetry.InstrumentAsync(nameof(DeleteAccountHandler), () => HandleCoreAsync(cmd, ct));

    private async Task<ErrorOr<DeleteAccountResponse>> HandleCoreAsync(DeleteAccountCommand cmd, CancellationToken ct)
    {
        var user = await db.Users.FindAsync([cmd.UserId], ct);
        if (user is null)
        {
            return UsersErrors.UserNotFound;
        }

        var userRef = new UserRef(user.Id.Value, user.DisplayName);

        var organizationCheck = await bus.InvokeAsync<ErrorOr<EnsureUserCanBeErasedFromOrganizationsResponse>>(
            new EnsureUserCanBeErasedFromOrganizationsCommand(user.Id.Value),
            ct);
        if (organizationCheck.IsError)
        {
            return organizationCheck.Errors;
        }

        if (!organizationCheck.Value.CanBeErased)
        {
            return new DeleteAccountResponse(organizationCheck.Value.BlockingOrganizations);
        }

        await eraser.EraseAsync(userRef, ErasureStrategy.HardDelete, ct);

        await bus.PublishAsync(new UserErasureRequestedV1(userRef.UserId, userRef.DisplayName, Guid.NewGuid()));

        return new DeleteAccountResponse([]);
    }
}
