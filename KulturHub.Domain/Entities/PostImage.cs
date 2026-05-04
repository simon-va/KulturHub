namespace KulturHub.Domain.Entities;

public class PostImage
{
    public Guid Id { get; set; }
    public Guid PostId { get; set; }
    public string StorageUrl { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
}
