using ErrorOr;
using FluentValidation;
using KulturHub.Application.Errors;
using KulturHub.Domain.Entities;
using KulturHub.Domain.Enums;
using KulturHub.Domain.Interfaces;

namespace KulturHub.Application.Features.Events.CreateEvent;

public class EventService(
    IEventRepository eventRepository,
    IChaynsApiClient chaynsApiClient,
    IValidator<CreateEventInput> validator) : IEventService
{
    public async Task<ErrorOr<Guid>> CreateEventAsync(CreateEventInput input, CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(input, cancellationToken);
        if (!validationResult.IsValid)
            throw new ValidationException(validationResult.Errors);

        var @event = Event.Create(
            input.Title,
            input.StartTime,
            input.EndTime,
            input.Address,
            input.Description);

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
