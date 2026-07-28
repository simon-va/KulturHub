using KulturHub.Domain.Users;

namespace KulturHub.Application.Ports;

public interface IUserReader
{
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
}