using KulturHub.Domain.Entities;

namespace KulturHub.Application.Ports;

public interface IAuthRepository
{
    Task<Invitation?> GetInvitationByCodeAsync(string code);
    Task InsertUserAsync(User user, Guid invitationId);
}
