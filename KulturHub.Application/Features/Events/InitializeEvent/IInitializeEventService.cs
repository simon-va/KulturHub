using ErrorOr;

namespace KulturHub.Application.Features.Events.InitializeEvent;

public interface IInitializeEventService
{
    Task<ErrorOr<Guid>> InitializeEventAsync(InitializeEventInput input);
}
