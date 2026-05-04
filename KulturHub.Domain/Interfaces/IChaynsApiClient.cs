using KulturHub.Domain.Models;

namespace KulturHub.Domain.Interfaces;

public interface IChaynsApiClient
{
    Task<List<ChaynsEvent>> GetEventsAsync(DateTime startDate, DateTime endDate);

    Task<int> CreateEventAsync(string title, DateTime startTime, DateTime endTime,
        string address, string description);
}
