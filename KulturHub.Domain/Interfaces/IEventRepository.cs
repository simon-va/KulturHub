using KulturHub.Domain.Entities;

namespace KulturHub.Domain.Interfaces;

public interface IEventRepository
{
    Task CreateAsync(Event @event);
    Task<IEnumerable<Event>> GetByOrganisationIdAsync(Guid organisationId);
    Task<Event?> GetByIdAsync(Guid eventId, Guid organisationId);
}
