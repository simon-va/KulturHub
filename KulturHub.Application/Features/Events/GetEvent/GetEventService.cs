using ErrorOr;
using KulturHub.Application.Errors;
using KulturHub.Application.Features.Events.GetEvents;
using KulturHub.Domain.Interfaces;

namespace KulturHub.Application.Features.Events.GetEvent;

public class GetEventService(IEventRepository eventRepository) : IGetEventService
{
    public async Task<ErrorOr<EventResponse>> GetEventAsync(GetEventInput input)
    {
        var @event = await eventRepository.GetByIdAsync(input.EventId, input.OrganisationId);
        if (@event is null)
            return EventErrors.NotFound(input.EventId);

        return new EventResponse(
            @event.Id,
            @event.OrganisationId,
            @event.Title,
            @event.StartTime,
            @event.EndTime,
            @event.Address,
            @event.Description,
            @event.CreatedAt,
            @event.Status,
            @event.ErrorMessage,
            @event.EventCategoryId,
            @event.ConversationId);
    }
}
