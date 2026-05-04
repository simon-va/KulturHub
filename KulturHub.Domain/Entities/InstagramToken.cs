namespace KulturHub.Domain.Entities;

public class InstagramToken
{
    public Guid Id { get; private set; }
    public string AccessToken { get; private set; } = string.Empty;
    public string InstagramUserId { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public DateTime LastRefreshedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public static InstagramToken Create(string accessToken, string instagramUserId, DateTime expiresAt) => new()
    {
        Id = Guid.NewGuid(),
        AccessToken = accessToken,
        InstagramUserId = instagramUserId,
        ExpiresAt = expiresAt,
        LastRefreshedAt = DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow,
    };

    public static InstagramToken Reconstitute(
        Guid id, string accessToken, string instagramUserId,
        DateTime expiresAt, DateTime lastRefreshedAt, DateTime createdAt) => new()
    {
        Id = id,
        AccessToken = accessToken,
        InstagramUserId = instagramUserId,
        ExpiresAt = expiresAt,
        LastRefreshedAt = lastRefreshedAt,
        CreatedAt = createdAt,
    };

    public void Refresh(string newAccessToken, DateTime newExpiresAt)
    {
        AccessToken = newAccessToken;
        ExpiresAt = newExpiresAt;
        LastRefreshedAt = DateTime.UtcNow;
    }
}
