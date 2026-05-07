using System.Data;
using Dapper;
using KulturHub.Domain.Entities;
using KulturHub.Domain.Interfaces;

namespace KulturHub.Infrastructure.Persistence.Repositories;

public class ConversationRepository : IConversationRepository
{
    public async Task CreateAsync(Conversation conversation, IDbTransaction transaction)
    {
        const string sql = """
            INSERT INTO conversations (id, organisation_id, created_at)
            VALUES (@Id, @OrganisationId, @CreatedAt)
            """;

        await transaction.Connection!.ExecuteAsync(sql, new
        {
            conversation.Id,
            conversation.OrganisationId,
            conversation.CreatedAt,
        }, transaction);
    }
}
