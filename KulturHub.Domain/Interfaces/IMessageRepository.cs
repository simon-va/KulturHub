using KulturHub.Domain.Entities;

namespace KulturHub.Domain.Interfaces;

public interface IMessageRepository
{
    Task CreateAsync(Message message);
}
