using Wolverine.Persistence.Durability;
using Wolverine.Persistence.Durability.DeadLetterManagement;

namespace Modulith.Api.Infrastructure.DeadLetters;

/// <summary>
/// Redacted projection of a Wolverine <see cref="DeadLetterEnvelope"/>.
/// The message body (<c>Envelope</c> and <c>Message</c> on the source type) is intentionally
/// excluded because integration events such as <c>PasswordResetRequestedV1</c>,
/// <c>EmailChangeRequestedV1</c>, and <c>ExternalLoginPendingV1</c> carry single-use
/// security tokens. Exposing the raw payload to any admin who can call this API would
/// allow token theft without touching the Users module.
/// </summary>
internal sealed record DeadLetterSummaryDto(
    Guid Id,
    string MessageType,
    string ReceivedAt,
    string Source,
    string ExceptionType,
    string ExceptionMessage,
    DateTimeOffset SentAt,
    DateTimeOffset? ExecutionTime,
    bool Replayable)
{
    internal static DeadLetterSummaryDto From(DeadLetterEnvelope e) => new(
        e.Id,
        e.MessageType,
        e.ReceivedAt,
        e.Source,
        e.ExceptionType,
        e.ExceptionMessage,
        e.SentAt,
        e.ExecutionTime,
        e.Replayable);
}

/// <summary>
/// Redacted projection of <see cref="DeadLetterEnvelopeResults"/>.
/// <c>DatabaseUri</c> is excluded to avoid leaking internal infrastructure details.
/// </summary>
internal sealed record DeadLetterSummaryResultsDto(
    int TotalCount,
    int PageNumber,
    IReadOnlyList<DeadLetterSummaryDto> Envelopes)
{
    internal static DeadLetterSummaryResultsDto From(DeadLetterEnvelopeResults r) => new(
        r.TotalCount,
        r.PageNumber,
        r.Envelopes.Select(DeadLetterSummaryDto.From).ToList());
}
