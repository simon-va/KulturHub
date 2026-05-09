using KulturHub.Domain.Entities;

namespace KulturHub.Domain.Interfaces;

public interface IEventCategoryRepository
{
    Task<IEnumerable<EventCategory>> GetAllAsync();
}
