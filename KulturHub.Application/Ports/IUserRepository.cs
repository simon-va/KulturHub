using KulturHub.Domain.Entities;

namespace KulturHub.Application.Ports;

public interface IUserRepository
{
    Task InsertUserAsync(User user, IUnitOfWorkTransaction? transaction = null, CancellationToken ct = default);

    Task<User?> GetByIdAsync(Guid userId, CancellationToken ct = default);

    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);

    Task<int> DeleteAsync(Guid userId, IUnitOfWorkTransaction? transaction = null, CancellationToken ct = default);

    Task<bool> IsAdminAsync(Guid userId, CancellationToken ct = default);
}
