using ErrorOr;
using KulturHub.Application.Features.Events.GetEvents;

namespace KulturHub.Application.Features.Events.GetEvent;

public interface IGetEventService
{
    Task<ErrorOr<EventResponse>> GetEventAsync(GetEventInput input);
}
