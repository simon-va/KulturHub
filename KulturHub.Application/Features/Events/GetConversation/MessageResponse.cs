using KulturHub.Domain.Enums;

namespace KulturHub.Application.Features.Events.GetConversation;

public record MessageResponse(Guid Id, MessageRole Role, string Content, DateTime CreatedAt);
