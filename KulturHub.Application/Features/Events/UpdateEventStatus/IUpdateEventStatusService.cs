using ErrorOr;

namespace KulturHub.Application.Features.Events.UpdateEventStatus;

public interface IUpdateEventStatusService
{
    Task<ErrorOr<Updated>> UpdateEventStatusAsync(UpdateEventStatusInput input);
}
