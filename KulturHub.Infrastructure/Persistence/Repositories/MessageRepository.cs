using System.Data;
using Dapper;
using KulturHub.Domain.Entities;
using KulturHub.Domain.Interfaces;

namespace KulturHub.Infrastructure.Persistence.Repositories;

public class MessageRepository : IMessageRepository
{
    public async Task CreateAsync(Message message, IDbTransaction transaction)
    {
        const string sql = """
            INSERT INTO messages (id, conversation_id, role, content, created_at)
            VALUES (@Id, @ConversationId, @Role, @Content, @CreatedAt)
            """;

        await transaction.Connection!.ExecuteAsync(sql, new
        {
            message.Id,
            message.ConversationId,
            Role = (int)message.Role,
            message.Content,
            message.CreatedAt,
        }, transaction);
    }
}
