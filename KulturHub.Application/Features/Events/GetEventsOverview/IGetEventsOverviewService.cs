using ErrorOr;

namespace KulturHub.Application.Features.Events.GetEventsOverview;

public interface IGetEventsOverviewService
{
    Task<ErrorOr<IEnumerable<EventOverviewResponse>>> GetEventsOverviewAsync(GetEventsOverviewInput input);
}
