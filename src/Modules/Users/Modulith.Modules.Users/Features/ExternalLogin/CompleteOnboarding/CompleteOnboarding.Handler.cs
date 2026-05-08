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
        var userId = new UserId(cmd.UserId);

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
        {
            return UsersErrors.UserNotFound;
        }

        if (!cmd.AcceptTerms)
        {
            return UsersErrors.TermsNotAccepted;
        }

        var now = clock.UtcNow;
        var tosKey = $"tos:{opts.TermsOfServiceVersion}";

        // Step 1: Save the ToS acceptance in isolation so that a concurrent unique-key
        // collision on (user_id, version) cannot roll back the consent or onboarding
        // writes that follow.
        var alreadyAccepted = await db.TermsAcceptances
            .AnyAsync(t => t.UserId == user.Id && t.Version == tosKey, ct);

        var trackerCleared = false;
        if (!alreadyAccepted)
        {
            db.TermsAcceptances.Add(TermsAcceptance.Record(user.Id, tosKey, now, cmd.IpAddress, cmd.UserAgent));
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation())
            {
                // A concurrent request won the race on terms_acceptances (user_id, version).
                // The row is present — clear the poisoned tracked entity and continue so
                // that the consent and onboarding writes below are not discarded.
                db.ChangeTracker.Clear();
                trackerCleared = true;
            }
        }

        // Step 2: If the tracker was cleared above, re-fetch the user so EF can
        // track the HasCompletedOnboarding mutation. The concurrent winner may have
        // already completed onboarding; CompleteOnboarding() is idempotent.
        if (trackerCleared)
        {
            user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
            if (user is null)
            {
                return UsersErrors.UserNotFound;
            }
        }

        if (cmd.AcceptMarketingEmails)
        {
            db.Consents.Add(Consent.Grant(user.Id.Value, ConsentKeys.MarketingEmail, now, cmd.IpAddress, cmd.UserAgent, opts.PrivacyPolicyVersion));
        }

        var wasAlreadyCompleted = user.HasCompletedOnboarding;

        var onboardingResult = user.CompleteOnboarding();
        if (onboardingResult.IsError)
        {
            return onboardingResult.Errors;
        }

        await db.SaveChangesAsync(ct);

        if (!wasAlreadyCompleted)
        {
            await bus.PublishAsync(new UserOnboardingCompletedV1(user.Id.Value, Guid.NewGuid()));
            UsersTelemetry.EventsPublished.Add(1, new KeyValuePair<string, object?>("event", nameof(UserOnboardingCompletedV1)));
        }

        return Result.Success;
    }
}
