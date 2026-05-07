using ErrorOr;
using KulturHub.Application.Errors;
using KulturHub.Application.Ports;
using KulturHub.Domain.Entities;
using KulturHub.Domain.Enums;
using KulturHub.Domain.Interfaces;

namespace KulturHub.Application.Features.Events.InitializeEvent;

public class InitializeEventService(
    IOrganisationRepository organisationRepository,
    IConversationRepository conversationRepository,
    IMessageRepository messageRepository,
    IEventRepository eventRepository,
    IUnitOfWork unitOfWork) : IInitializeEventService
{
    public async Task<ErrorOr<Guid>> InitializeEventAsync(InitializeEventInput input)
    {
        var isMember = await organisationRepository.IsMemberAsync(input.OrganisationId, input.UserId);
        if (!isMember)
            return OrganisationErrors.Forbidden();

        var conversation = Conversation.Create(input.OrganisationId);
        var message = Message.Create(conversation.Id, MessageRole.System, "Neue Veranstaltung erstellt. Erzähl mir von ihr.");
        var @event = Event.CreateDraft(input.OrganisationId, conversation.Id);

        await unitOfWork.BeginAsync();
        await conversationRepository.CreateAsync(conversation);
        await messageRepository.CreateAsync(message);
        await eventRepository.CreateAsync(@event);
        await unitOfWork.CommitAsync();

        return @event.Id;
    }
}
