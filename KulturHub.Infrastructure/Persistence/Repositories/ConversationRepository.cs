using Dapper;
using KulturHub.Application.Ports;
using KulturHub.Domain.Entities;
using KulturHub.Domain.Interfaces;

namespace KulturHub.Infrastructure.Persistence.Repositories;

public class ConversationRepository(IConnectionProvider connectionProvider) : IConversationRepository
{
    public async Task CreateAsync(Conversation conversation)
    {
        const string sql = """
            INSERT INTO conversations (id, organisation_id, created_at)
            VALUES (@Id, @OrganisationId, @CreatedAt)
            """;

        await connectionProvider.Connection.ExecuteAsync(sql, new
        {
            conversation.Id,
            conversation.OrganisationId,
            conversation.CreatedAt,
        }, connectionProvider.Transaction);
    }

    public async Task DeleteAsync(Guid conversationId)
    {
        const string deleteMessages = """
            DELETE FROM messages
            WHERE conversation_id = @ConversationId
            """;

        const string deleteConversation = """
            DELETE FROM conversations
            WHERE id = @ConversationId
            """;

        var param = new { ConversationId = conversationId };
        await connectionProvider.Connection.ExecuteAsync(deleteMessages, param, connectionProvider.Transaction);
        await connectionProvider.Connection.ExecuteAsync(deleteConversation, param, connectionProvider.Transaction);
    }
}
