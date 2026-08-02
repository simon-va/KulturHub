using ErrorOr;
using KulturHub.Application.Abstractions.Pagination;
using KulturHub.Application.Abstractions.Persistence;
using KulturHub.Domain.Organisations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KulturHub.Application.Features.Platform.ChangeLogs.ListChangeLogs;

public sealed class ListChangeLogsHandler(
    IAppDbContext db,
    ILogger<ListChangeLogsHandler> logger)
{
    public async Task<ErrorOr<PagedResult<ChangeLogResponse>>> HandleAsync(
        ListChangeLogsCommand command,
        CancellationToken cancellationToken)
    {
        var orgId = OrganisationId.From(command.OrganisationId);
        var trimmedSearch = command.Search?.Trim();
        var hasSearch = !string.IsNullOrWhiteSpace(trimmedSearch);

        IQueryable<Domain.ChangeLogs.ChangeLog> filteredQuery = db.ChangeLogs.AsNoTracking()
            .Where(c => c.OrganisationId == orgId);

        if (command.Category is { } categoryFilter)
            filteredQuery = filteredQuery.Where(c => c.Category == categoryFilter);

        if (hasSearch)
        {
            var loweredSearch = trimmedSearch!.ToLower();
            filteredQuery = filteredQuery
                .Join(
                    db.Users.IgnoreQueryFilters().AsNoTracking(),
                    c => c.CreatedBy,
                    u => u.Id,
                    (c, u) => new { ChangeLog = c, User = u })
                .Where(x =>
                    x.ChangeLog.Message.ToLower().Contains(loweredSearch) ||
                    x.User.FirstName.ToLower().Contains(loweredSearch) ||
                    x.User.LastName.ToLower().Contains(loweredSearch))
                .Select(x => x.ChangeLog);
        }

        var total = await filteredQuery.CountAsync(cancellationToken);

        var items = await filteredQuery
            .OrderByDescending(c => c.CreatedAt)
            .Skip(command.Skip)
            .Take(command.Take)
            .Join(
                db.Users.IgnoreQueryFilters().AsNoTracking(),
                c => c.CreatedBy,
                u => u.Id,
                (c, u) => new ChangeLogResponse(
                    c.Id.Value,
                    c.CreatedBy.Value,
                    CreatedByFullName: $"{u.FirstName} {u.LastName}",
                    c.Message,
                    c.Category,
                    c.Data,
                    c.CreatedAt))
            .ToListAsync(cancellationToken);

        logger.LogInformation(
            "Listed change logs for organisation {OrganisationId}: {Count} of {Total} (skip={Skip}, take={Take}, search={Search}, category={Category})",
            command.OrganisationId, items.Count, total, command.Skip, command.Take,
            hasSearch ? trimmedSearch : "<none>",
            command.Category?.ToString() ?? "<none>");

        return new PagedResult<ChangeLogResponse>(items, total, command.Skip, command.Take);
    }
}