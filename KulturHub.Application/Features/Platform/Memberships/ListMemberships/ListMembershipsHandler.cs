using ErrorOr;
using KulturHub.Application.Abstractions.Persistence;
using KulturHub.Domain.Organisations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KulturHub.Application.Features.Platform.Memberships.ListMemberships;

public sealed class ListMembershipsHandler(
    IAppDbContext db,
    ILogger<ListMembershipsHandler> logger)
{
    public async Task<ErrorOr<IReadOnlyList<MembershipResponse>>> HandleAsync(
        Guid organisationId,
        CancellationToken cancellationToken)
    {
        var memberships = await db.Memberships
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(m => m.OrganisationId == OrganisationId.From(organisationId) && !m.IsDeleted)
            .Join(
                db.Users.IgnoreQueryFilters().AsNoTracking(),
                m => m.UserId,
                u => u.Id,
                (m, u) => new MembershipResponse(
                    m.Id.Value,
                    m.UserId.Value,
                    FullName: $"{u.FirstName} {u.LastName}",
                    u.Email,
                    m.InvitedAt,
                    m.DecidedAt,
                    m.Status))
            .ToListAsync(cancellationToken);

        logger.LogInformation(
            "Listed memberships for organisation {OrganisationId}: {Count} found",
            organisationId, memberships.Count);

        return memberships;
    }
}