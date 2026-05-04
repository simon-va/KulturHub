namespace KulturHub.Domain.Interfaces;

public interface IInstagramTokenRefresher
{
    Task<(string AccessToken, DateTime ExpiresAt)> RefreshAsync(string currentAccessToken, CancellationToken cancellationToken = default);
}
