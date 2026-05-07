using ErrorOr;

namespace KulturHub.Application.Features.Events.GetEvents;

public interface IGetEventsService
{
    Task<ErrorOr<IEnumerable<EventResponse>>> GetEventsAsync(GetEventsInput input);
}
