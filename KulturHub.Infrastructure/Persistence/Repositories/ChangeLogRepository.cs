using Dapper;
using KulturHub.Application.Features.ChangeLogs.ListOrganisationChangeLogs;
using KulturHub.Application.Ports;
using KulturHub.Domain.Entities;
using KulturHub.Infrastructure.Persistence.Internal;
using KulturHub.Infrastructure.Persistence.Mappings;
using KulturHub.Infrastructure.Persistence.Models;

namespace KulturHub.Infrastructure.Persistence.Repositories;

public class ChangeLogRepository(IDbConnectionFactory connectionFactory) : IChangeLogRepository
{
    public async Task InsertAsync(
        ChangeLog changeLog,
        IUnitOfWorkTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO change_logs (id, organisation_id, user_id, message, data, created_at)
            VALUES (@Id, @OrganisationId, @UserId, @Message, @Data::jsonb, @CreatedAt)
            """;

        var parameters = new
        {
            changeLog.Id,
            changeLog.OrganisationId,
            changeLog.UserId,
            changeLog.Message,
            Data = ChangeLogMapper.SerializeData(changeLog.Data),
            changeLog.CreatedAt,
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

    public async Task<IReadOnlyList<ChangeLogListItem>> ListByOrganisationAsync(
        Guid organisationId,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                c.id              AS Id,
                c.organisation_id AS OrganisationId,
                c.user_id         AS UserId,
                c.message         AS Message,
                c.data            AS Data,
                c.created_at      AS CreatedAt,
                u.first_name      AS UserFirstName,
                u.last_name       AS UserLastName
            FROM change_logs c
            INNER JOIN users u ON u.user_id = c.user_id
            WHERE c.organisation_id = @OrganisationId
            ORDER BY c.created_at DESC, c.id DESC
            LIMIT @Take OFFSET @Skip
            """;

        using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<ChangeLogReadRow>(new CommandDefinition(
            sql,
            new { OrganisationId = organisationId, Skip = skip, Take = take },
            cancellationToken: cancellationToken));

        return rows.Select(ChangeLogReadMapper.ToListItem).ToList();
    }
}
