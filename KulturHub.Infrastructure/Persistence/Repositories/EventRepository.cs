using Dapper;
using KulturHub.Domain.Entities;
using KulturHub.Domain.Interfaces;

namespace KulturHub.Infrastructure.Persistence.Repositories;

public class EventRepository(IDbConnectionFactory connectionFactory) : IEventRepository
{
    public async Task CreateAsync(Event @event)
    {
        const string sql = """
            INSERT INTO events (id, organisation_id, title, start_time, end_time, address, description, created_at, status)
            VALUES (@Id, @OrganisationId, @Title, @StartTime, @EndTime, @Address, @Description, @CreatedAt, @Status)
            """;

        using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync();
        await connection.ExecuteAsync(sql, new
        {
            @event.Id,
            @event.OrganisationId,
            @event.Title,
            @event.StartTime,
            @event.EndTime,
            @event.Address,
            @event.Description,
            @event.CreatedAt,
            Status = @event.Status.ToString(),
        });
    }
}
