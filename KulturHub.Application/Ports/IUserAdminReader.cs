namespace KulturHub.Application.Ports;

public interface IUserAdminReader
{
    Task<bool> IsAdminAsync(Guid userId, CancellationToken cancellationToken = default);
}