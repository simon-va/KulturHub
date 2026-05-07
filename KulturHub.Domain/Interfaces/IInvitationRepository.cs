using KulturHub.Domain.Entities;

namespace KulturHub.Domain.Interfaces;

public interface IInvitationRepository
{
    Task<Invitation?> GetByCodeAsync(string code);
    Task CreateAsync(Invitation invitation);
}
