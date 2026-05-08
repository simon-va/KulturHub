using ErrorOr;
using KulturHub.Application.Errors;
using KulturHub.Domain.Interfaces;

namespace KulturHub.Application.Features.Events.GetConversation;

public class GetConversationService(
    IEventRepository eventRepository,
    IMessageRepository messageRepository) : IGetConversationService
{
    public async Task<ErrorOr<ConversationResponse>> GetConversationAsync(GetConversationInput input)
    {
        var @event = await eventRepository.GetByIdAsync(input.EventId, input.OrganisationId);
        if (@event is null) return EventErrors.NotFound(input.EventId);

        if (@event.ConversationId is null) return EventErrors.NoConversation(input.EventId);

        var messages = await messageRepository.GetByConversationIdAsync(@event.ConversationId.Value);
        var messageResponses = messages.Select(m => new MessageResponse(m.Id, m.Role, m.Content, m.CreatedAt));

        return new ConversationResponse(@event.ConversationId.Value, messageResponses);
    }
}
