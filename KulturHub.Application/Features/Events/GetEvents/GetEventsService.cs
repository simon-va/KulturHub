using ErrorOr;
using KulturHub.Application.Errors;
using KulturHub.Domain.Interfaces;

namespace KulturHub.Application.Features.Events.GetEvents;

public class GetEventsService(
    IOrganisationRepository organisationRepository,
    IEventRepository eventRepository) : IGetEventsService
{
    public async Task<ErrorOr<IEnumerable<EventResponse>>> GetEventsAsync(GetEventsInput input)
    {
        var isMember = await organisationRepository.IsMemberAsync(input.OrganisationId, input.UserId);
        if (!isMember)
            return OrganisationErrors.Forbidden();

        var events = await eventRepository.GetByOrganisationIdAsync(input.OrganisationId);

        return events
            .Select(e => new EventResponse(
                e.Id,
                e.OrganisationId,
                e.Title,
                e.StartTime,
                e.EndTime,
                e.Address,
                e.Description,
                e.CreatedAt,
                e.Status,
                e.ErrorMessage,
                e.EventCategoryId,
                e.ConversationId))
            .ToList();
    }
}
