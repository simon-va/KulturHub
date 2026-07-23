using KulturHub.Domain.Entities;

namespace KulturHub.Application.Ports;

public interface IOrganisationRepository
{
    Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken = default);

    Task<Organisation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task InsertAsync(
        Organisation organisation,
        IUnitOfWorkTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        Organisation organisation,
        IUnitOfWorkTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Organisation>> ListByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<int> SoftDeleteAsync(
        Guid id,
        IUnitOfWorkTransaction? transaction = null,
        CancellationToken cancellationToken = default);
}
