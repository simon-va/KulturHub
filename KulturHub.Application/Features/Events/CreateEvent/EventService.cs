using ErrorOr;
using FluentValidation;
using KulturHub.Application.Errors;
using KulturHub.Domain.Entities;
using KulturHub.Domain.Interfaces;

namespace KulturHub.Application.Features.Events.CreateEvent;

public class EventService(
    IEventRepository eventRepository,
    IOrganisationRepository organisationRepository,
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

        return @event.Id;
    }
}
