using KulturHub.Domain.Entities;

namespace KulturHub.Domain.Interfaces;

public interface IInstagramTokenRepository
{
    Task<InstagramToken?> GetCurrentTokenAsync();
    Task UpdateTokenAsync(InstagramToken token);
    Task CreateTokenAsync(InstagramToken token);
}
