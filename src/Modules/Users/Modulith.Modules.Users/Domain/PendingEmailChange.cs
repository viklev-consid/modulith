using Modulith.Shared.Kernel.Domain;

namespace Modulith.Modules.Users.Domain;

/// <summary>
/// Holds the requested-but-unconfirmed new email address alongside the
/// <see cref="SingleUseToken"/> that must be verified to complete the change.
/// One record per user maximum; overwritten on repeated requests.
/// </summary>
public sealed class PendingEmailChange : Entity<PendingEmailChangeId>
{
    private PendingEmailChange(
        PendingEmailChangeId id,
        UserId userId,
        Email newEmail,
        SingleUseTokenId tokenId) : base(id)
    {
        UserId = userId;
        NewEmail = newEmail;
        TokenId = tokenId;
    }

    // Required by EF Core for materialization.
    private PendingEmailChange() : base(new PendingEmailChangeId(Guid.Empty)) { }

    public UserId UserId { get; private set; } = null!;
    public Email NewEmail { get; private set; } = null!;
    public SingleUseTokenId TokenId { get; private set; } = null!;

    public static PendingEmailChange Create(
        UserId userId,
        Email newEmail,
        SingleUseTokenId tokenId) =>
        new(PendingEmailChangeId.New(), userId, newEmail, tokenId);
}
