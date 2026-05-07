using Dapper;
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
}
