namespace KulturHub.Application.Ports;

public interface IInstagramTokenRefresher
{
    Task<(string AccessToken, DateTime ExpiresAt)> RefreshAsync(string currentAccessToken, CancellationToken cancellationToken = default);
}
