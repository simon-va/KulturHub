using Dapper;
using KulturHub.Domain.Entities;
using KulturHub.Domain.Interfaces;

namespace KulturHub.Infrastructure.Persistence.Repositories;

public class MessageRepository(IConnectionProvider connectionProvider) : IMessageRepository
{
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
}
