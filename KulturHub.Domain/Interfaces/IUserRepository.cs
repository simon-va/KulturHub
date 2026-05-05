using KulturHub.Domain.Entities;

namespace KulturHub.Domain.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid userId);
}
