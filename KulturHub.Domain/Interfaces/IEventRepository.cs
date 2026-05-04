using KulturHub.Domain.Entities;
using KulturHub.Domain.Enums;

namespace KulturHub.Domain.Interfaces;

public interface IEventRepository
{
    Task CreateAsync(Event @event);
    Task UpdateStatusAsync(Guid id, EventStatus status, int? chaynsEventId = null, string? errorMessage = null);
}
