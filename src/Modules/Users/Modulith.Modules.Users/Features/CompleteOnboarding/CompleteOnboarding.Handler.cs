using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Modulith.Modules.Users.Contracts;
using Modulith.Modules.Users.Contracts.Events;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Errors;
using Modulith.Modules.Users.Legal;
using Modulith.Modules.Users.Persistence;
using Modulith.Shared.Kernel.Interfaces;
using Wolverine;

namespace Modulith.Modules.Users.Features.CompleteOnboarding;

public sealed class CompleteOnboardingHandler(
    UsersDbContext db,
    IOptions<UsersOptions> options,
    IMessageBus bus,
    IClock clock,
    ILegalComplianceService complianceService)
{
    public async Task<ErrorOr<Success>> Handle(CompleteOnboardingCommand cmd, CancellationToken ct)
        => await UsersTelemetry.InstrumentAsync(nameof(CompleteOnboardingHandler), () => HandleCoreAsync(cmd, ct));

    private async Task<ErrorOr<Success>> HandleCoreAsync(CompleteOnboardingCommand cmd, CancellationToken ct)
    {
        var optionsValue = options.Value;
        var userId = new UserId(cmd.UserId);

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
        {
            return UsersErrors.UserNotFound;
        }

        if (cmd.AcceptedDocuments.Count == 0)
        {
            return UsersErrors.TermsNotAccepted;
        }

        var now = clock.UtcNow;
        var requiredDocuments = await db.LegalDocuments
            .Where(d => d.IsRequiredForOnboarding && d.SupersededAt == null)
            .ToListAsync(ct);

        if (requiredDocuments.Count == 0)
        {
            return UsersErrors.LegalDocumentsUnavailable;
        }

        var validationResult = ValidateAcceptedDocuments(requiredDocuments, cmd.AcceptedDocuments);
        if (validationResult.IsError)
        {
            return validationResult.Errors;
        }

        var requiredDocumentIds = requiredDocuments.Select(d => d.Id).ToArray();
        var alreadyAccepted = await db.TermsAcceptances
            .Where(t => t.UserId == user.Id && t.LegalDocumentId != null && requiredDocumentIds.Contains(t.LegalDocumentId!))
            .Select(t => t.LegalDocumentId!.Value)
            .ToHashSetAsync(ct);

        foreach (var document in requiredDocuments.Where(d => !alreadyAccepted.Contains(d.Id.Value)))
        {
            db.TermsAcceptances.Add(TermsAcceptance.Record(user.Id, document, now, cmd.IpAddress, cmd.UserAgent));
        }

        if (cmd.AcceptMarketingEmails)
        {
            db.Consents.Add(Consent.Grant(
                user.Id.Value,
                ConsentKeys.MarketingEmail,
                now,
                cmd.IpAddress,
                cmd.UserAgent,
                requiredDocuments.FirstOrDefault(d => d.DocumentType == LegalDocumentType.PrivacyPolicy)?.Version
                    ?? optionsValue.PrivacyPolicyVersion));
        }

        var wasAlreadyCompleted = user.HasCompletedOnboarding;

        var onboardingResult = user.CompleteOnboarding();
        if (onboardingResult.IsError)
        {
            return onboardingResult.Errors;
        }

        await db.SaveChangesAsync(ct);
        await complianceService.InvalidateContinuedUseComplianceAsync(user.Id, ct);

        if (!wasAlreadyCompleted)
        {
            await bus.PublishAsync(new UserOnboardingCompletedV1(user.Id.Value, Guid.NewGuid()));
            UsersTelemetry.EventsPublished.Add(1, new KeyValuePair<string, object?>("event", nameof(UserOnboardingCompletedV1)));
        }

        return Result.Success;
    }

    private static ErrorOr<Success> ValidateAcceptedDocuments(
        IReadOnlyCollection<LegalDocument> requiredDocuments,
        IReadOnlyCollection<AcceptedLegalDocumentCommand> acceptedDocuments)
    {
        var acceptedById = acceptedDocuments
            .GroupBy(d => d.DocumentId)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var document in requiredDocuments)
        {
            if (!acceptedById.TryGetValue(document.Id.Value, out var accepted))
            {
                return UsersErrors.RequiredLegalDocumentMissing;
            }

            if (!string.Equals(accepted.Version, document.Version, StringComparison.Ordinal) ||
                !string.Equals(accepted.ContentHash, document.ContentHash, StringComparison.Ordinal))
            {
                return UsersErrors.LegalDocumentAcceptanceInvalid;
            }
        }

        return Result.Success;
    }
}
