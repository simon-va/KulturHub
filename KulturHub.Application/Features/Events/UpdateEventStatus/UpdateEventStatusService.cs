using ErrorOr;
using KulturHub.Application.Errors;
using KulturHub.Application.Ports;
using KulturHub.Domain.Enums;
using KulturHub.Domain.Interfaces;

namespace KulturHub.Application.Features.Events.UpdateEventStatus;

public class UpdateEventStatusService(
    IOrganisationRepository organisationRepository,
    IEventRepository eventRepository,
    IUnitOfWork unitOfWork) : IUpdateEventStatusService
{
    public async Task<ErrorOr<Updated>> UpdateEventStatusAsync(UpdateEventStatusInput input)
    {
        var isMember = await organisationRepository.IsMemberAsync(input.OrganisationId, input.UserId);
        if (!isMember) return OrganisationErrors.Forbidden();

        if (input.Status == EventStatus.Failed)
            return EventErrors.InvalidStatus();

        var @event = await eventRepository.GetByIdAsync(input.EventId, input.OrganisationId);
        if (@event is null) return EventErrors.NotFound(input.EventId);

        @event.SetStatus(input.Status);

        await unitOfWork.BeginAsync();
        await eventRepository.UpdateStatusAsync(@event);
        await unitOfWork.CommitAsync();

        return Result.Updated;
    }
}
