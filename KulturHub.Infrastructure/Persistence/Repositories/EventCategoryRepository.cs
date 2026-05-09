using Dapper;
using KulturHub.Application.Ports;
using KulturHub.Domain.Entities;
using KulturHub.Domain.Interfaces;

namespace KulturHub.Infrastructure.Persistence.Repositories;

public class EventCategoryRepository(IDbConnectionFactory connectionFactory) : IEventCategoryRepository
{
    public async Task<IEnumerable<EventCategory>> GetAllAsync()
    {
        const string sql = """
            SELECT id    AS Id,
                   name  AS Name,
                   color AS Color
            FROM event_categories
            ORDER BY name
            """;

        using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync();
        return await connection.QueryAsync<EventCategory>(sql);
    }
}
