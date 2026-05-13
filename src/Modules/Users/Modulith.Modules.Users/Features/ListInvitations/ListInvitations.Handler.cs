using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Users.Errors;
using Modulith.Modules.Users.Persistence;
using Modulith.Shared.Kernel.Interfaces;
using Modulith.Shared.Kernel.Pagination;

namespace Modulith.Modules.Users.Features.ListInvitations;

public sealed class ListInvitationsHandler(UsersDbContext db, IClock clock)
{
    private const string Pending = "pending";
    private const string Expired = "expired";
    private const string Revoked = "revoked";
    private const string Accepted = "accepted";
    private const string All = "all";

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
            ? Pending
            : query.Status.Trim().ToLowerInvariant();

        if (normalizedStatus is not (Pending or Expired or Revoked or Accepted or All))
        {
            return UsersErrors.InvitationStatusInvalid;
        }

        var now = clock.UtcNow;
        var pagination = PageRequest.Of(query.Page, query.PageSize);
        var invitationsQuery = db.UserInvitations.AsNoTracking();

        invitationsQuery = normalizedStatus switch
        {
            Pending => invitationsQuery.Where(i => i.IsPending && i.ExpiresAt > now),
            Expired => invitationsQuery.Where(i => i.IsPending && i.ExpiresAt <= now),
            Revoked => invitationsQuery.Where(i => i.RevokedAt != null),
            Accepted => invitationsQuery.Where(i => i.AcceptedAt != null),
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
            return Accepted;
        }

        if (revokedAt is not null)
        {
            return Revoked;
        }

        return expiresAt <= now ? Expired : Pending;
    }
}
