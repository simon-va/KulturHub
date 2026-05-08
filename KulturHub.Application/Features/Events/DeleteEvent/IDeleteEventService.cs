using ErrorOr;

namespace KulturHub.Application.Features.Events.DeleteEvent;

public interface IDeleteEventService
{
    Task<ErrorOr<Deleted>> DeleteEventAsync(DeleteEventInput input);
}
