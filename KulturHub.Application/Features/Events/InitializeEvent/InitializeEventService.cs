using ErrorOr;
using KulturHub.Application.Errors;
using KulturHub.Domain.Entities;
using KulturHub.Domain.Enums;
using KulturHub.Domain.Interfaces;

namespace KulturHub.Application.Features.Events.InitializeEvent;

public class InitializeEventService(
    IConversationRepository conversationRepository,
    IMessageRepository messageRepository,
    IEventRepository eventRepository) : IInitializeEventService
{
    public async Task<ErrorOr<Guid>> InitializeEventAsync(InitializeEventInput input)
    {
        var conversation = Conversation.Create(input.OrganisationId);
        var message = Message.Create(conversation.Id, MessageRole.Assistant, "Neue Veranstaltung erstellt. Erzähl mir von ihr.");
        var @event = Event.CreateDraft(input.OrganisationId, conversation.Id);

        await conversationRepository.CreateAsync(conversation);
        await messageRepository.CreateAsync(message);
        await eventRepository.CreateAsync(@event);

        return @event.Id;
    }
}
