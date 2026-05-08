using KulturHub.Application.Features.Events.GetConversation;

namespace KulturHub.Application.Features.Events.SendMessage;

public record SendMessageResponse(MessageResponse UserMessage, MessageResponse BotMessage);
