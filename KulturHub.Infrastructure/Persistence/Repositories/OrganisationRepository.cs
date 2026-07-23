using Dapper;
using KulturHub.Application.Ports;
using KulturHub.Domain.Entities;
using KulturHub.Infrastructure.Persistence.Internal;
using KulturHub.Infrastructure.Persistence.Mappings;

namespace KulturHub.Infrastructure.Persistence.Repositories;

public class OrganisationRepository(IDbConnectionFactory connectionFactory) : IOrganisationRepository
{
    public async Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM organisations
                WHERE name = @Name
                  AND is_deleted = FALSE
            )
            """;

        using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            sql,
            new { Name = name },
            cancellationToken: cancellationToken));
    }

    public async Task InsertAsync(
        Organisation organisation,
        IUnitOfWorkTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO organisations (id, name, created_at)
            VALUES (@Id, @Name, @CreatedAt)
            """;

        var parameters = new
        {
            organisation.Id,
            organisation.Name,
            organisation.CreatedAt,
        };

        if (transaction is null)
        {
            using var connection = connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
            return;
        }

        var (conn, tx) = transaction.Unwrap();
        await conn.ExecuteAsync(new CommandDefinition(sql, parameters, transaction: tx, cancellationToken: cancellationToken));
    }

    public async Task<Organisation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
              id,
              name,
              created_at,
              is_deleted,
              deleted_at
            FROM organisations
            WHERE id = @Id
              AND is_deleted = FALSE
            """;

        using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<OrganisationMapper.OrganisationRow>(new CommandDefinition(
            sql,
            new { Id = id },
            cancellationToken: cancellationToken));

        return row is null ? null : OrganisationMapper.ToEntity(row);
    }

    public async Task UpdateAsync(
        Organisation organisation,
        IUnitOfWorkTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE organisations
            SET name = @Name
            WHERE id = @Id
              AND is_deleted = FALSE
            """;

        var parameters = new
        {
            organisation.Id,
            organisation.Name,
        };

        if (transaction is null)
        {
            using var connection = connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
            return;
        }

        var (conn, tx) = transaction.Unwrap();
        await conn.ExecuteAsync(new CommandDefinition(sql, parameters, transaction: tx, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<Organisation>> ListByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
              o.id,
              o.name,
              o.created_at,
              o.is_deleted,
              o.deleted_at
            FROM organisations o
            INNER JOIN memberships m ON m.organisation_id = o.id
            WHERE m.user_id = @UserId
              AND o.is_deleted = FALSE
              AND m.is_deleted = FALSE
            ORDER BY o.name ASC
            """;

        using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<OrganisationMapper.OrganisationRow>(new CommandDefinition(
            sql,
            new { UserId = userId },
            cancellationToken: cancellationToken));

        return rows.Select(OrganisationMapper.ToEntity).ToList();
    }

    public async Task<int> SoftDeleteAsync(
        Guid id,
        IUnitOfWorkTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE organisations
            SET is_deleted = TRUE,
                deleted_at = NOW()
            WHERE id = @Id
              AND is_deleted = FALSE
            """;

        if (transaction is null)
        {
            using var connection = connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            return await connection.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
        }

        var (conn, tx) = transaction.Unwrap();
        return await conn.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, transaction: tx, cancellationToken: cancellationToken));
    }
}
