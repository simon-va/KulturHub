using KulturHub.Domain.Enums;

namespace KulturHub.Domain.Entities;

public class Post
{
    private readonly List<PostImage> _images = [];

    public Guid Id { get; private set; }
    public PostType Type { get; private set; }
    public PostStatus Status { get; private set; }
    public string Caption { get; private set; } = string.Empty;
    public string? ErrorMessage { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? PublishedAt { get; private set; }
    public string? InstagramMediaId { get; private set; }
    public IReadOnlyList<PostImage> Images => _images.AsReadOnly();

    public static Post CreateWeeklyPost(DateTime weekStart, DateTime weekEnd) => new()
    {
        Id = Guid.NewGuid(),
        Type = PostType.WeeklyOverview,
        Status = PostStatus.Draft,
        Caption = $"Veranstaltungen vom {weekStart:dd.MM} bis {weekEnd:dd.MM.yyyy}",
        CreatedAt = DateTime.UtcNow
    };

    public static Post Reconstitute(
        Guid id, PostType type, PostStatus status, string caption,
        string? errorMessage, DateTime createdAt, DateTime? publishedAt,
        string? instagramMediaId) => new()
    {
        Id = id,
        Type = type,
        Status = status,
        Caption = caption,
        ErrorMessage = errorMessage,
        CreatedAt = createdAt,
        PublishedAt = publishedAt,
        InstagramMediaId = instagramMediaId,
    };

    public void AddImages(IEnumerable<PostImage> images) => _images.AddRange(images);

    public void MarkAsUploading() => Status = PostStatus.Uploading;

    public void AddImage(PostImage image) => _images.Add(image);

    public void Publish(string mediaId)
    {
        Status = PostStatus.Published;
        InstagramMediaId = mediaId;
        PublishedAt = DateTime.UtcNow;
    }

    public void MarkAsFailed(string message)
    {
        Status = PostStatus.Failed;
        ErrorMessage = message;
    }
}
