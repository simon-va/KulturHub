using KulturHub.Application.Features.ChangeLogs.ListOrganisationChangeLogs;
using KulturHub.Domain.Entities;

namespace KulturHub.Application.Ports;

public interface IChangeLogRepository
{
    Task InsertAsync(
        ChangeLog changeLog,
        IUnitOfWorkTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChangeLogListItem>> ListByOrganisationAsync(
        Guid organisationId,
        int skip,
        int take,
        CancellationToken cancellationToken = default);
}
