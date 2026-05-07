using Dapper;
using KulturHub.Domain.Entities;
using KulturHub.Domain.Interfaces;

namespace KulturHub.Infrastructure.Persistence.Repositories;

public class EventRepository(IConnectionProvider connectionProvider) : IEventRepository
{
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
}
