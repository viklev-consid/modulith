using ErrorOr;
using Modulith.Modules.Users.Errors;
using Modulith.Modules.Users.Gdpr;
using Modulith.Modules.Users.Persistence;
using Modulith.Shared.Kernel.Gdpr;

namespace Modulith.Modules.Users.Features.DeleteAccount;

public sealed class DeleteAccountHandler(
    UsersDbContext db,
    PersonalDataOrchestrator orchestrator)
{
    public async Task<ErrorOr<Deleted>> Handle(DeleteAccountCommand cmd, CancellationToken ct)
    {
        var user = await db.Users.FindAsync([cmd.UserId], ct);
        if (user is null)
        {
            return UsersErrors.UserNotFound;
        }

        var userRef = new UserRef(user.Id.Value, user.DisplayName);

        foreach (var eraser in orchestrator.Erasers)
        {
            await eraser.EraseAsync(userRef, ErasureStrategy.HardDelete, ct);
        }

        return Result.Deleted;
    }
}
