using System.Data;
using Dapper;
using KulturHub.Domain.Entities;
using KulturHub.Domain.Interfaces;

namespace KulturHub.Infrastructure.Persistence.Repositories;

public class EventRepository : IEventRepository
{
    public async Task CreateAsync(Event @event, IDbTransaction transaction)
    {
        const string sql = """
            INSERT INTO events (id, organisation_id, title, start_time, end_time, address, description, created_at, status, event_category_id, conversation_id)
            VALUES (@Id, @OrganisationId, @Title, @StartTime, @EndTime, @Address, @Description, @CreatedAt, @Status, @EventCategoryId, @ConversationId)
            """;

        await transaction.Connection!.ExecuteAsync(sql, new
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
            @event.EventCategoryId,
            @event.ConversationId,
        }, transaction);
    }
}
