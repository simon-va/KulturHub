using ErrorOr;
using KulturHub.Application.Abstractions.Persistence;
using KulturHub.Domain.Memberships;
using KulturHub.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KulturHub.Application.Features.Platform.Organisations.ListMyOrganisations;

public sealed class ListMyOrganisationsHandler(
    IAppDbContext db,
    ILogger<ListMyOrganisationsHandler> logger)
{
    public async Task<ErrorOr<IReadOnlyList<MyOrganisationResponse>>> HandleAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var organisations = await db.Memberships
            .AsNoTracking()
            .Where(m => m.UserId == UserId.From(userId)
                && m.Status == MembershipStatus.Accepted)
            .Join(
                db.Organisations.AsNoTracking(),
                m => m.OrganisationId,
                o => o.Id,
                (m, o) => new MyOrganisationResponse(o.Id.Value, o.Name))
            .ToListAsync(cancellationToken);

        var sorted = organisations
            .OrderBy(o => o.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        logger.LogInformation(
            "Listed organisations for user {UserId}: {Count} found",
            userId, sorted.Count);

        return sorted;
    }
}
