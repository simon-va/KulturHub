namespace KulturHub.Application.Features.Events.GetConversation;

public record ConversationResponse(Guid ConversationId, IEnumerable<MessageResponse> Messages);
