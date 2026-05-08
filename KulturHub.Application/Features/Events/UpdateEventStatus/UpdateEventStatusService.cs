using ErrorOr;
using KulturHub.Application.Errors;
using KulturHub.Domain.Enums;
using KulturHub.Domain.Exceptions;
using KulturHub.Domain.Interfaces;

namespace KulturHub.Application.Features.Events.UpdateEventStatus;

public class UpdateEventStatusService(
    IEventRepository eventRepository) : IUpdateEventStatusService
{
    public async Task<ErrorOr<Updated>> UpdateEventStatusAsync(UpdateEventStatusInput input)
    {
        if (input.Status == EventStatus.Failed)
            return EventErrors.InvalidTransition("any", "Failed");

        var @event = await eventRepository.GetByIdAsync(input.EventId, input.OrganisationId);
        if (@event is null) return EventErrors.NotFound(input.EventId);

        try
        {
            switch (input.Status)
            {
                case EventStatus.Published:
                    @event.Publish();
                    break;
                case EventStatus.ReadyToPublish:
                    @event.MarkReadyToPublish();
                    break;
                default:
                    return EventErrors.InvalidTransition(@event.Status.ToString(), input.Status.ToString());
            }
        }
        catch (DomainException)
        {
            return EventErrors.InvalidTransition(@event.Status.ToString(), input.Status.ToString());
        }

        await eventRepository.UpdateStatusAsync(@event);

        return Result.Updated;
    }
}
