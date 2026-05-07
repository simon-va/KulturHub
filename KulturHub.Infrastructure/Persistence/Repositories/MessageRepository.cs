using Dapper;
using KulturHub.Application.Ports;
using KulturHub.Domain.Entities;
using KulturHub.Domain.Enums;
using KulturHub.Domain.Interfaces;

namespace KulturHub.Infrastructure.Persistence.Repositories;

public class MessageRepository(
    IConnectionProvider connectionProvider,
    IDbConnectionFactory connectionFactory) : IMessageRepository
{
    private sealed record MessageRow(Guid Id, Guid ConversationId, int Role, string Content, DateTime CreatedAt);

    public async Task CreateAsync(Message message)
    {
        const string sql = """
            INSERT INTO messages (id, conversation_id, role, content, created_at)
            VALUES (@Id, @ConversationId, @Role, @Content, @CreatedAt)
            """;

        await connectionProvider.Connection.ExecuteAsync(sql, new
        {
            message.Id,
            message.ConversationId,
            Role = (int)message.Role,
            message.Content,
            message.CreatedAt,
        }, connectionProvider.Transaction);
    }

    public async Task<IEnumerable<Message>> GetByConversationIdAsync(Guid conversationId)
    {
        const string sql = """
            SELECT id              AS Id,
                   conversation_id AS ConversationId,
                   role            AS Role,
                   content         AS Content,
                   created_at      AS CreatedAt
            FROM messages
            WHERE conversation_id = @ConversationId
            ORDER BY created_at ASC
            """;

        using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync();
        var rows = await connection.QueryAsync<MessageRow>(sql, new { ConversationId = conversationId });
        return rows.Select(r => Message.Reconstitute(r.Id, r.ConversationId, (MessageRole)r.Role, r.Content, r.CreatedAt));
    }
}
