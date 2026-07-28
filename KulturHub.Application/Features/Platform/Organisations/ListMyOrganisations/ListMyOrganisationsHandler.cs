using ErrorOr;
using KulturHub.Application.Abstractions.Persistence;
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
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(m => m.UserId == UserId.From(userId) && !m.IsDeleted)
            .Join(
                db.Organisations.IgnoreQueryFilters().AsNoTracking(),
                m => m.OrganisationId,
                o => o.Id,
                (m, o) => new { o.Id, o.Name, o.IsDeleted })
            .Where(x => !x.IsDeleted)
            .Select(x => new MyOrganisationResponse(x.Id.Value, x.Name))
            .ToListAsync(cancellationToken);

        logger.LogInformation(
            "Listed organisations for user {UserId}: {Count} found",
            userId, organisations.Count);

        return organisations;
    }
}
