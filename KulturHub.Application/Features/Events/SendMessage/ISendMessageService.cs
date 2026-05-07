using ErrorOr;
using KulturHub.Application.Features.Events.GetConversation;

namespace KulturHub.Application.Features.Events.SendMessage;

public interface ISendMessageService
{
    Task<ErrorOr<MessageResponse>> SendMessageAsync(SendMessageInput input, CancellationToken cancellationToken = default);
}
