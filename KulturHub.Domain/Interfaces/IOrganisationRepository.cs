using KulturHub.Domain.Entities;

namespace KulturHub.Domain.Interfaces;

public interface IOrganisationRepository
{
    Task CreateAsync(Organisation organisation, Guid userId);
    Task<bool> UpdateAsync(Guid id, string name);
    Task<bool> DeleteAsync(Guid id);
    Task<bool> IsMemberAsync(Guid organisationId, Guid userId);
    Task<IEnumerable<Organisation>> GetByUserIdAsync(Guid userId);
}
