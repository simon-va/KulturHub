namespace KulturHub.Application.Ports;

public interface IUserAdminClient
{
    Task<bool> DeleteUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
