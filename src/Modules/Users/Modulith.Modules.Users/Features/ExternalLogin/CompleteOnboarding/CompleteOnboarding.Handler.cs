using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Modulith.Modules.Users.Contracts.Events;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Errors;
using Modulith.Modules.Users.Persistence;
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

        if (!string.Equals(cmd.TermsVersion, opts.TermsOfServiceVersion, StringComparison.Ordinal))
        {
            return Error.Validation("Users.Onboarding.TermsVersionMismatch",
                $"Expected ToS version '{opts.TermsOfServiceVersion}'.");
        }

        if (!string.Equals(cmd.PrivacyPolicyVersion, opts.PrivacyPolicyVersion, StringComparison.Ordinal))
        {
            return Error.Validation("Users.Onboarding.PrivacyVersionMismatch",
                $"Expected Privacy Policy version '{opts.PrivacyPolicyVersion}'.");
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == new UserId(cmd.UserId), ct);
        if (user is null)
        {
            return UsersErrors.UserNotFound;
        }

        var now = clock.UtcNow;

        db.TermsAcceptances.Add(TermsAcceptance.Record(user.Id, $"tos:{cmd.TermsVersion}", now, cmd.IpAddress, cmd.UserAgent));
        db.TermsAcceptances.Add(TermsAcceptance.Record(user.Id, $"pp:{cmd.PrivacyPolicyVersion}", now, cmd.IpAddress, cmd.UserAgent));

        var onboardingResult = user.CompleteOnboarding();
        if (onboardingResult.IsError)
        {
            return onboardingResult.Errors;
        }

        await db.SaveChangesAsync(ct);

        await bus.PublishAsync(new UserOnboardingCompletedV1(user.Id.Value, Guid.NewGuid()));
        UsersTelemetry.EventsPublished.Add(1, new KeyValuePair<string, object?>("event", nameof(UserOnboardingCompletedV1)));

        return Result.Success;
    }
}
