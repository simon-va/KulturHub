using ErrorOr;
using KulturHub.Application.Abstractions.Persistence;
using KulturHub.Domain.Memberships;
using KulturHub.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KulturHub.Application.Features.Platform.Memberships.ListMyPendingMemberships;

public sealed class ListMyPendingMembershipsHandler(
    IAppDbContext db,
    ILogger<ListMyPendingMembershipsHandler> logger)
{
    public async Task<ErrorOr<IReadOnlyList<PendingMembershipResponse>>> HandleAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var pending = await db.Memberships
            .AsNoTracking()
            .Where(m => m.UserId == UserId.From(userId)
                && m.Status == MembershipStatus.Pending)
            .Join(
                db.Organisations.AsNoTracking(),
                m => m.OrganisationId,
                o => o.Id,
                (m, o) => new PendingMembershipResponse(
                    m.Id.Value,
                    o.Id.Value,
                    o.Name))
            .ToListAsync(cancellationToken);

        var sorted = pending
            .OrderBy(p => p.OrganisationName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        logger.LogInformation(
            "Listed pending memberships for user {UserId}: {Count} found",
            userId, sorted.Count);

        return sorted;
    }
}