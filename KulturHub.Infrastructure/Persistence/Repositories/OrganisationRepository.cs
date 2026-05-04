using Dapper;
using KulturHub.Domain.Entities;
using KulturHub.Domain.Interfaces;

namespace KulturHub.Infrastructure.Persistence.Repositories;

public class OrganisationRepository(IDbConnectionFactory connectionFactory) : IOrganisationRepository
{
    public async Task CreateAsync(Organisation organisation, Guid userId)
    {
        const string insertOrganisation = """
            INSERT INTO organisations (id, name)
            VALUES (@Id, @Name)
            """;

        const string insertMember = """
            INSERT INTO organisation_members (id, user_id, organisation_id)
            VALUES (@Id, @UserId, @OrganisationId)
            """;

        using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync();
        using var transaction = await connection.BeginTransactionAsync();

        await connection.ExecuteAsync(insertOrganisation, new
        {
            organisation.Id,
            organisation.Name,
        }, transaction);

        await connection.ExecuteAsync(insertMember, new
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OrganisationId = organisation.Id,
        }, transaction);

        await transaction.CommitAsync();
    }

    public async Task<bool> UpdateAsync(Guid id, string name)
    {
        const string sql = """
            UPDATE organisations
            SET name = @Name
            WHERE id = @Id
            """;

        using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync();
        var rows = await connection.ExecuteAsync(sql, new { Id = id, Name = name });
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        const string sql = """
            DELETE FROM organisations
            WHERE id = @Id
            """;

        using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync();
        var rows = await connection.ExecuteAsync(sql, new { Id = id });
        return rows > 0;
    }

    public async Task<bool> IsMemberAsync(Guid organisationId, Guid userId)
    {
        const string sql = """
            SELECT 1 FROM organisation_members
            WHERE organisation_id = @OrganisationId AND user_id = @UserId
            LIMIT 1
            """;

        using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync();
        var result = await connection.QueryFirstOrDefaultAsync<int?>(sql, new { OrganisationId = organisationId, UserId = userId });
        return result.HasValue;
    }
}
