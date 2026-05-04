using ErrorOr;
using KulturHub.Application.Features.Events.CreateEvent;

namespace KulturHub.Application.Features.Events;

public interface IEventService
{
    Task<ErrorOr<Guid>> CreateEventAsync(CreateEventInput input);
}
