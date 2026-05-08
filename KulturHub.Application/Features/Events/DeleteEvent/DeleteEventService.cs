using ErrorOr;
using KulturHub.Application.Errors;
using KulturHub.Domain.Interfaces;

namespace KulturHub.Application.Features.Events.DeleteEvent;

public class DeleteEventService(
    IEventRepository eventRepository,
    IConversationRepository conversationRepository) : IDeleteEventService
{
    public async Task<ErrorOr<Deleted>> DeleteEventAsync(DeleteEventInput input)
    {
        var @event = await eventRepository.GetByIdAsync(input.EventId, input.OrganisationId);
        if (@event is null)
            return EventErrors.NotFound(input.EventId);

        await eventRepository.DeleteAsync(input.EventId, input.OrganisationId);

        if (@event.ConversationId.HasValue)
            await conversationRepository.DeleteAsync(@event.ConversationId.Value);

        return Result.Deleted;
    }
}
