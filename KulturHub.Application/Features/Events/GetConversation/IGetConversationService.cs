using ErrorOr;

namespace KulturHub.Application.Features.Events.GetConversation;

public interface IGetConversationService
{
    Task<ErrorOr<ConversationResponse>> GetConversationAsync(GetConversationInput input);
}
