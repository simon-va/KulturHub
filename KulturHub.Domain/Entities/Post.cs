using KulturHub.Domain.Enums;

namespace KulturHub.Domain.Entities;

public class Post
{
    public Guid Id { get; set; }
    public PostType Type { get; set; }
    public PostStatus Status { get; set; }
    public string Caption { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public string? InstagramMediaId { get; set; }
    public List<PostImage> Images { get; set; } = [];

    public static Post CreateWeeklyPost(DateTime weekStart, DateTime weekEnd) => new()
    {
        Id = Guid.NewGuid(),
        Type = PostType.WeeklyOverview,
        Status = PostStatus.Draft,
        Caption = $"Veranstaltungen vom {weekStart:dd.MM} bis {weekEnd:dd.MM.yyyy}",
        CreatedAt = DateTime.UtcNow
    };

    public void MarkAsUploading() => Status = PostStatus.Uploading;

    public void AddImage(PostImage image) => Images.Add(image);

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
