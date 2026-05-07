using KulturHub.Domain.Enums;

namespace KulturHub.Domain.Entities;

public class Message
{
    public Guid Id { get; private set; }
    public Guid ConversationId { get; private set; }
    public MessageRole Role { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }

    public static Message Create(Guid conversationId, MessageRole role, string content) => new()
    {
        Id = Guid.NewGuid(),
        ConversationId = conversationId,
        Role = role,
        Content = content,
        CreatedAt = DateTime.UtcNow,
    };
}
