using ErrorOr;
using FluentValidation;
using KulturHub.Application.Errors;
using KulturHub.Domain.Entities;
using KulturHub.Domain.Enums;
using KulturHub.Application.Ports;
using KulturHub.Domain.Interfaces;

namespace KulturHub.Application.Features.Events.CreateEvent;

public class EventService(
    IEventRepository eventRepository,
    IOrganisationRepository organisationRepository,
    IChaynsApiClient chaynsApiClient,
    IValidator<CreateEventInput> validator) : IEventService
{
    public async Task<ErrorOr<Guid>> CreateEventAsync(CreateEventInput input)
    {
        var validationResult = await validator.ValidateAsync(input);
        if (!validationResult.IsValid)
            return validationResult.Errors
                .Select(e => Error.Validation(e.PropertyName, e.ErrorMessage))
                .ToList();

        var isMember = await organisationRepository.IsMemberAsync(input.OrganisationId, input.UserId);
        if (!isMember)
            return OrganisationErrors.Forbidden();

        var @event = Event.Create(
            input.OrganisationId,
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
