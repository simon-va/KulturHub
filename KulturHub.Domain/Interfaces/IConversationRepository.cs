using KulturHub.Domain.Entities;

namespace KulturHub.Domain.Interfaces;

public interface IConversationRepository
{
    Task CreateAsync(Conversation conversation);
}
