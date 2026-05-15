using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Users.Errors;
using Modulith.Modules.Users.Persistence;
using Modulith.Shared.Kernel.Interfaces;
using Modulith.Shared.Kernel.Pagination;

namespace Modulith.Modules.Users.Features.ListInvitations;

public sealed class ListInvitationsHandler(UsersDbContext db, IClock clock)
{
    private const string pending = "pending";
    private const string expired = "expired";
    private const string revoked = "revoked";
    private const string accepted = "accepted";
    private const string all = "all";

    public async Task<ErrorOr<ListInvitationsResponse>> Handle(ListInvitationsQuery query, CancellationToken ct)
        => await UsersTelemetry.InstrumentAsync(nameof(ListInvitationsHandler), () => HandleCoreAsync(query, ct));

    private async Task<ErrorOr<ListInvitationsResponse>> HandleCoreAsync(ListInvitationsQuery query, CancellationToken ct)
    {
        if (query.Page <= 0 || query.Page > PageRequest.MaxPage)
        {
            return UsersErrors.PageInvalid;
        }

        if (query.PageSize <= 0 || query.PageSize > PageRequest.MaxPageSize)
        {
            return UsersErrors.PageSizeInvalid;
        }

        var normalizedStatus = string.IsNullOrWhiteSpace(query.Status)
            ? pending
            : query.Status.Trim().ToLowerInvariant();

        if (normalizedStatus is not (pending or expired or revoked or accepted or all))
        {
            return UsersErrors.InvitationStatusInvalid;
        }

        var now = clock.UtcNow;
        var pagination = PageRequest.Of(query.Page, query.PageSize);
        var invitationsQuery = db.UserInvitations.AsNoTracking();

        invitationsQuery = normalizedStatus switch
        {
            pending => invitationsQuery.Where(i => i.IsPending && i.ExpiresAt > now),
            expired => invitationsQuery.Where(i => i.IsPending && i.ExpiresAt <= now),
            revoked => invitationsQuery.Where(i => i.RevokedAt != null),
            accepted => invitationsQuery.Where(i => i.AcceptedAt != null),
            _ => invitationsQuery
        };

        var total = await invitationsQuery.CountAsync(ct);
        var invitationRows = await invitationsQuery
            .OrderByDescending(i => i.InvitedAt)
            .Skip(pagination.Offset)
            .Take(pagination.PageSize)
            .Select(i => new
            {
                i.Id.Value,
                i.Email,
                i.InvitedAt,
                i.ExpiresAt,
                i.AcceptedAt,
                i.RevokedAt
            })
            .ToListAsync(ct);

        var invitations = invitationRows
            .Select(i => new ListInvitationsInvitationDto(
                i.Value,
                i.Email,
                GetStatus(i.AcceptedAt, i.RevokedAt, i.ExpiresAt, now),
                i.InvitedAt,
                i.ExpiresAt,
                i.AcceptedAt,
                i.RevokedAt))
            .ToList();

        return new ListInvitationsResponse(invitations, pagination.Page, pagination.PageSize, total);
    }

    private static string GetStatus(
        DateTimeOffset? acceptedAt,
        DateTimeOffset? revokedAt,
        DateTimeOffset expiresAt,
        DateTimeOffset now)
    {
        if (acceptedAt is not null)
        {
            return accepted;
        }

        if (revokedAt is not null)
        {
            return revoked;
        }

        return expiresAt <= now ? expired : pending;
    }
}
