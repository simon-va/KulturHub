namespace KulturHub.Domain.Entities;

public class InstagramToken
{
    public Guid Id { get; set; }
    public string AccessToken { get; set; } = string.Empty;
    public string InstagramUserId { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime LastRefreshedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
