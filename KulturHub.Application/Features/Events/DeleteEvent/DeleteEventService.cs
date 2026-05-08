using ErrorOr;
using KulturHub.Application.Errors;
using KulturHub.Application.Ports;
using KulturHub.Domain.Interfaces;

namespace KulturHub.Application.Features.Events.DeleteEvent;

public class DeleteEventService(
    IOrganisationRepository organisationRepository,
    IEventRepository eventRepository,
    IConversationRepository conversationRepository,
    IUnitOfWork unitOfWork) : IDeleteEventService
{
    public async Task<ErrorOr<Deleted>> DeleteEventAsync(DeleteEventInput input)
    {
        var isMember = await organisationRepository.IsMemberAsync(input.OrganisationId, input.UserId);
        if (!isMember)
            return OrganisationErrors.Forbidden();

        var @event = await eventRepository.GetByIdAsync(input.EventId, input.OrganisationId);
        if (@event is null)
            return EventErrors.NotFound(input.EventId);

        await unitOfWork.BeginAsync();

        await eventRepository.DeleteAsync(input.EventId, input.OrganisationId);

        if (@event.ConversationId.HasValue)
            await conversationRepository.DeleteAsync(@event.ConversationId.Value);

        await unitOfWork.CommitAsync();

        return Result.Deleted;
    }
}
