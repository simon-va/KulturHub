using ErrorOr;
using KulturHub.Application.Errors;
using KulturHub.Domain.Entities;
using KulturHub.Domain.Enums;
using KulturHub.Domain.Interfaces;
using MediatR;

namespace KulturHub.Application.Features.Events.CreateEvent;

public class CreateEventHandler(
    IEventRepository eventRepository,
    IChaynsApiClient chaynsApiClient) : IRequestHandler<CreateEventCommand, ErrorOr<Guid>>
{
    public async Task<ErrorOr<Guid>> Handle(CreateEventCommand request, CancellationToken cancellationToken)
    {
        var @event = Event.Create(
            request.Title,
            request.StartTime,
            request.EndTime,
            request.Address,
            request.Description);

        await eventRepository.CreateAsync(@event);

        try
        {
            int chaynsEventId = await chaynsApiClient.CreateEventAsync(
                @event.Title,
                @event.StartTime,
                @event.EndTime,
                @event.Address,
                @event.Description);

            await eventRepository.UpdateStatusAsync(@event.Id, EventStatus.Published, chaynsEventId);
        }
        catch (Exception ex)
        {
            await eventRepository.UpdateStatusAsync(@event.Id, EventStatus.Failed, errorMessage: ex.Message);
            return EventErrors.ChaynsCreateFailed(ex.Message);
        }

        return @event.Id;
    }
}
