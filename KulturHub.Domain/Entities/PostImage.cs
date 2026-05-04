namespace KulturHub.Domain.Entities;

public class PostImage
{
    public Guid Id { get; init; }
    public Guid PostId { get; init; }
    public string StorageUrl { get; init; } = string.Empty;
    public int SortOrder { get; init; }
    public DateTime CreatedAt { get; init; }

    public static PostImage Create(Guid postId, string storageUrl, int sortOrder) => new()
    {
        Id = Guid.NewGuid(),
        PostId = postId,
        StorageUrl = storageUrl,
        SortOrder = sortOrder,
        CreatedAt = DateTime.UtcNow,
    };
}
