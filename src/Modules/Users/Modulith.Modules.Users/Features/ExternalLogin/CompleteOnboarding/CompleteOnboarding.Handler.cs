using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Modulith.Modules.Users.Contracts;
using Modulith.Modules.Users.Contracts.Events;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Errors;
using Modulith.Modules.Users.Persistence;
using Modulith.Shared.Infrastructure.Persistence;
using Modulith.Shared.Kernel.Interfaces;
using Wolverine;

namespace Modulith.Modules.Users.Features.ExternalLogin.CompleteOnboarding;

public sealed class CompleteOnboardingHandler(
    UsersDbContext db,
    IOptions<UsersOptions> options,
    IMessageBus bus,
    IClock clock)
{
    public async Task<ErrorOr<Success>> Handle(CompleteOnboardingCommand cmd, CancellationToken ct)
        => await UsersTelemetry.InstrumentAsync(nameof(CompleteOnboardingHandler), () => HandleCoreAsync(cmd, ct));

    private async Task<ErrorOr<Success>> HandleCoreAsync(CompleteOnboardingCommand cmd, CancellationToken ct)
    {
        var opts = options.Value;

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == new UserId(cmd.UserId), ct);
        if (user is null)
        {
            return UsersErrors.UserNotFound;
        }

        var now = clock.UtcNow;

        var tosKey = $"tos:{opts.TermsOfServiceVersion}";
        var alreadyAccepted = await db.TermsAcceptances
            .AnyAsync(t => t.UserId == user.Id && t.Version == tosKey, ct);

        if (!alreadyAccepted)
        {
            db.TermsAcceptances.Add(TermsAcceptance.Record(user.Id, tosKey, now, cmd.IpAddress, cmd.UserAgent));
        }

        if (cmd.AcceptMarketingEmails)
        {
            db.Consents.Add(Consent.Grant(user.Id.Value, ConsentKeys.MarketingEmail, now, cmd.IpAddress, cmd.UserAgent));
        }

        var onboardingResult = user.CompleteOnboarding();
        if (onboardingResult.IsError)
        {
            return onboardingResult.Errors;
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation())
        {
            // A concurrent request already inserted the ToS row and completed onboarding.
            // The unique constraint on (UserId, Version) fired, but the outcome is the same —
            // treat this as a successful idempotent no-op rather than a 500.
            return Result.Success;
        }

        await bus.PublishAsync(new UserOnboardingCompletedV1(user.Id.Value, Guid.NewGuid()));
        UsersTelemetry.EventsPublished.Add(1, new KeyValuePair<string, object?>("event", nameof(UserOnboardingCompletedV1)));

        return Result.Success;
    }
}
