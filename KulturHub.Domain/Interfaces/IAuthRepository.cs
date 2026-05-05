using KulturHub.Domain.Entities;

namespace KulturHub.Domain.Interfaces;

public interface IAuthRepository
{
    Task InsertUserAsync(User user, Guid invitationId);
}
