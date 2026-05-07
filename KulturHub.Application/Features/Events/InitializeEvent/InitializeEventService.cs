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
    IDbConnectionFactory connectionFactory) : IInitializeEventService
{
    public async Task<ErrorOr<Guid>> InitializeEventAsync(InitializeEventInput input)
    {
        var isMember = await organisationRepository.IsMemberAsync(input.OrganisationId, input.UserId);
        if (!isMember)
            return OrganisationErrors.Forbidden();

        var conversation = Conversation.Create(input.OrganisationId);
        var message = Message.Create(conversation.Id, MessageRole.System, "Neue Veranstaltung erstellt. Erzähl mir von ihr.");
        var @event = Event.CreateDraft(input.OrganisationId, conversation.Id);

        using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync();
        using var transaction = await connection.BeginTransactionAsync();

        await conversationRepository.CreateAsync(conversation, transaction);
        await messageRepository.CreateAsync(message, transaction);
        await eventRepository.CreateAsync(@event, transaction);

        await transaction.CommitAsync();

        return @event.Id;
    }
}
