using Dapper;
using KulturHub.Application.Ports;
using KulturHub.Domain.Entities;
using KulturHub.Domain.Enums;
using KulturHub.Domain.Interfaces;

namespace KulturHub.Infrastructure.Persistence.Repositories;

public class EventRepository(
    IConnectionProvider connectionProvider,
    IDbConnectionFactory connectionFactory) : IEventRepository
{
    private sealed record EventRow(
        Guid Id, Guid OrganisationId, string Title,
        DateTime? StartTime, DateTime? EndTime,
        string Address, string Description, DateTime CreatedAt,
        int Status, string? ErrorMessage,
        Guid? EventCategoryId, Guid? ConversationId);

    public async Task CreateAsync(Event @event)
    {
        const string sql = """
            INSERT INTO events (id, organisation_id, title, start_time, end_time, address, description, created_at, status, error_message, event_category_id, conversation_id)
            VALUES (@Id, @OrganisationId, @Title, @StartTime, @EndTime, @Address, @Description, @CreatedAt, @Status, @ErrorMessage, @EventCategoryId, @ConversationId)
            """;

        await connectionProvider.Connection.ExecuteAsync(sql, new
        {
            @event.Id,
            @event.OrganisationId,
            @event.Title,
            @event.StartTime,
            @event.EndTime,
            @event.Address,
            @event.Description,
            @event.CreatedAt,
            Status = (int)@event.Status,
            @event.ErrorMessage,
            @event.EventCategoryId,
            @event.ConversationId,
        }, connectionProvider.Transaction);
    }

    public async Task<IEnumerable<Event>> GetByOrganisationIdAsync(Guid organisationId)
    {
        const string sql = """
            SELECT id               AS Id,
                   organisation_id  AS OrganisationId,
                   title            AS Title,
                   start_time       AS StartTime,
                   end_time         AS EndTime,
                   address          AS Address,
                   description      AS Description,
                   created_at       AS CreatedAt,
                   status           AS Status,
                   error_message    AS ErrorMessage,
                   event_category_id AS EventCategoryId,
                   conversation_id  AS ConversationId
            FROM events
            WHERE organisation_id = @OrganisationId
            ORDER BY created_at DESC
            """;

        using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync();
        var rows = await connection.QueryAsync<EventRow>(sql, new { OrganisationId = organisationId });
        return rows.Select(r => Event.Reconstitute(
            r.Id, r.OrganisationId, r.Title,
            r.StartTime, r.EndTime,
            r.Address, r.Description, r.CreatedAt,
            (EventStatus)r.Status, r.ErrorMessage,
            r.EventCategoryId, r.ConversationId));
    }

    public async Task UpdateDraftAsync(Event @event)
    {
        const string sql = """
            UPDATE events
            SET title       = @Title,
                address     = @Address,
                description = @Description,
                start_time  = @StartTime,
                end_time    = @EndTime,
                status      = @Status
            WHERE id = @Id
            """;

        await connectionProvider.Connection.ExecuteAsync(sql, new
        {
            @event.Id,
            @event.Title,
            @event.Address,
            @event.Description,
            @event.StartTime,
            @event.EndTime,
            Status = (int)@event.Status,
        }, connectionProvider.Transaction);
    }

    public async Task<Event?> GetByIdAsync(Guid eventId, Guid organisationId)
    {
        const string sql = """
            SELECT id               AS Id,
                   organisation_id  AS OrganisationId,
                   title            AS Title,
                   start_time       AS StartTime,
                   end_time         AS EndTime,
                   address          AS Address,
                   description      AS Description,
                   created_at       AS CreatedAt,
                   status           AS Status,
                   error_message    AS ErrorMessage,
                   event_category_id AS EventCategoryId,
                   conversation_id  AS ConversationId
            FROM events
            WHERE id = @EventId AND organisation_id = @OrganisationId
            """;

        using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync();
        var row = await connection.QuerySingleOrDefaultAsync<EventRow>(sql, new { EventId = eventId, OrganisationId = organisationId });
        if (row is null) return null;
        return Event.Reconstitute(
            row.Id, row.OrganisationId, row.Title,
            row.StartTime, row.EndTime,
            row.Address, row.Description, row.CreatedAt,
            (EventStatus)row.Status, row.ErrorMessage,
            row.EventCategoryId, row.ConversationId);
    }
}
