using KulturHub.Application.Abstractions.Persistence;
using KulturHub.Application.Ports;
using KulturHub.Domain.Memberships;
using KulturHub.Domain.Organisations;
using KulturHub.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace KulturHub.Infrastructure.Persistence;

public sealed class MembershipReader(IAppDbContext db) : IMembershipReader
{
    public Task<bool> IsMemberAsync(
        Guid userId,
        Guid organisationId,
        CancellationToken cancellationToken) =>
        db.Memberships
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(
                m => m.UserId == UserId.From(userId)
                    && m.OrganisationId == OrganisationId.From(organisationId)
                    && !m.IsDeleted
                    && m.Status == MembershipStatus.Accepted,
                cancellationToken);
}
