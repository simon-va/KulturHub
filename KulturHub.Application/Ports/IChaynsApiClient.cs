using KulturHub.Application.Features.WeeklyPost;

namespace KulturHub.Application.Ports;

public interface IChaynsApiClient
{
    Task<List<ChaynsEvent>> GetEventsAsync(DateTime startDate, DateTime endDate);
}
