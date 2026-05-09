using ErrorOr;
using KulturHub.Domain.Interfaces;

namespace KulturHub.Application.Features.Events.GetEventsOverview;

public class GetEventsOverviewService(IEventRepository eventRepository) : IGetEventsOverviewService
{
    public async Task<ErrorOr<IEnumerable<EventOverviewResponse>>> GetEventsOverviewAsync(GetEventsOverviewInput input)
    {
        var events = await eventRepository.GetByOrganisationIdAsync(input.OrganisationId);

        return events
            .Select(e => new EventOverviewResponse(
                e.Id,
                e.Title,
                e.StartTime,
                e.Status))
            .ToList();
    }
}
