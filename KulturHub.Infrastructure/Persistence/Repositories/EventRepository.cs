using Dapper;
using KulturHub.Domain.Entities;
using KulturHub.Domain.Enums;
using KulturHub.Domain.Interfaces;

namespace KulturHub.Infrastructure.Persistence.Repositories;

public class EventRepository(IDbConnectionFactory connectionFactory) : IEventRepository
{
    public async Task CreateAsync(Event @event)
    {
        const string sql = """
            INSERT INTO events (id, title, start_time, end_time, address, description, created_at, chayns_event_id, status)
            VALUES (@Id, @Title, @StartTime, @EndTime, @Address, @Description, @CreatedAt, @ChaynsEventId, @Status)
            """;

        using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync();
        await connection.ExecuteAsync(sql, new
        {
            @event.Id,
            @event.Title,
            @event.StartTime,
            @event.EndTime,
            @event.Address,
            @event.Description,
            @event.CreatedAt,
            @event.ChaynsEventId,
            Status = @event.Status.ToString(),
        });
    }

    public async Task UpdateStatusAsync(Guid id, EventStatus status, int? chaynsEventId = null, string? errorMessage = null)
    {
        const string sql = """
            UPDATE events
            SET status = @Status,
                chayns_event_id = COALESCE(@ChaynsEventId, chayns_event_id),
                error_message = @ErrorMessage
            WHERE id = @Id
            """;

        using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync();
        await connection.ExecuteAsync(sql, new
        {
            Id = id,
            Status = status.ToString(),
            ChaynsEventId = chaynsEventId,
            ErrorMessage = errorMessage,
        });
    }
}
