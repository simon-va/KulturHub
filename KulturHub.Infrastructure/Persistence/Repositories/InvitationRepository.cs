using Dapper;
using ErrorOr;
using KulturHub.Application.Errors;
using KulturHub.Application.Features.Invitations.ListInvitations;
using KulturHub.Application.Ports;
using KulturHub.Domain.Entities;
using KulturHub.Infrastructure.Persistence.Internal;
using KulturHub.Infrastructure.Persistence.Mappings;
using KulturHub.Infrastructure.Persistence.Models;

namespace KulturHub.Infrastructure.Persistence.Repositories;

public class InvitationRepository(IDbConnectionFactory connectionFactory) : IInvitationRepository
{
    public async Task<Invitation?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = """
            SELECT
              id, code, used_by, created_at, expires_at,
              is_deleted, deleted_at
            FROM invitations
            WHERE id = @Id
              AND is_deleted = FALSE
            """;

        using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync<InvitationMapper.InvitationRow>(new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));
        return row is null ? null : InvitationMapper.ToEntity(row);
    }

    public async Task<Invitation?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        const string sql = """
            SELECT
              id, code, used_by, created_at, expires_at,
              is_deleted, deleted_at
            FROM invitations
            WHERE code = @Code
              AND is_deleted = FALSE
            """;

        using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync<InvitationMapper.InvitationRow>(new CommandDefinition(sql, new { Code = code }, cancellationToken: ct));
        return row is null ? null : InvitationMapper.ToEntity(row);
    }

    public async Task<IReadOnlyList<InvitationListItem>> ListAsync(
        InvitationFilter filter,
        CancellationToken ct = default)
    {
        const string sql = """
            SELECT
              i.id            AS Id,
              i.code          AS Code,
              i.created_at    AS CreatedAt,
              i.expires_at    AS ExpiresAt,
              (i.used_by IS NOT NULL)  AS IsUsed,
              (i.expires_at <= NOW())  AS IsExpired,
              i.used_by       AS UsedById,
              u.first_name    AS UsedByFirstName,
              u.last_name     AS UsedByLastName
            FROM invitations i
            LEFT JOIN users u ON u.user_id = i.used_by AND u.is_deleted = FALSE
            WHERE i.is_deleted = FALSE
              AND (@IncludeUsed = TRUE OR i.used_by IS NULL)
              AND (@IncludeExpired = TRUE OR i.expires_at > NOW())
            ORDER BY i.created_at DESC
            """;

        using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(ct);
        var rows = await connection.QueryAsync<InvitationReadRow>(
            new CommandDefinition(
                sql,
                new { filter.IncludeUsed, filter.IncludeExpired },
                cancellationToken: ct));

        return rows.Select(InvitationReadMapper.ToListItem).ToList();
    }

    public async Task InsertAsync(
        Invitation invitation,
        IUnitOfWorkTransaction? transaction = null,
        CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO invitations (id, code, used_by, created_at, expires_at)
            VALUES (@Id, @Code, @UsedBy, @CreatedAt, @ExpiresAt)
            """;

        var parameters = new
        {
            invitation.Id,
            invitation.Code,
            UsedBy = invitation.UsedBy,
            invitation.CreatedAt,
            invitation.ExpiresAt,
        };

        if (transaction is null)
        {
            using var connection = connectionFactory.CreateConnection();
            await connection.OpenAsync(ct);
            await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: ct));
            return;
        }

        var (conn, tx) = transaction.Unwrap();
        await conn.ExecuteAsync(new CommandDefinition(sql, parameters, transaction: tx, cancellationToken: ct));
    }

    public async Task<ErrorOr<Success>> MarkAsUsedAsync(
        Guid id,
        Guid userId,
        IUnitOfWorkTransaction? transaction = null,
        CancellationToken ct = default)
    {
        const string sql = """
            UPDATE invitations
            SET used_by = @UsedBy
            WHERE id = @Id
              AND used_by IS NULL
              AND is_deleted = FALSE
            """;

        var parameters = new { Id = id, UsedBy = userId };

        int rows;
        if (transaction is null)
        {
            using var connection = connectionFactory.CreateConnection();
            await connection.OpenAsync(ct);
            rows = await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: ct));
        }
        else
        {
            var (conn, tx) = transaction.Unwrap();
            rows = await conn.ExecuteAsync(new CommandDefinition(sql, parameters, transaction: tx, cancellationToken: ct));
        }

        return rows == 0 ? InvitationErrors.AlreadyUsed : Result.Success;
    }

    public async Task<int> DeleteAsync(
        Guid id,
        IUnitOfWorkTransaction? transaction = null,
        CancellationToken ct = default)
    {
        const string sql = """
            UPDATE invitations
            SET is_deleted = TRUE,
                deleted_at = NOW()
            WHERE id = @Id
              AND is_deleted = FALSE
            """;

        var parameters = new { Id = id };

        if (transaction is null)
        {
            using var connection = connectionFactory.CreateConnection();
            await connection.OpenAsync(ct);
            return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: ct));
        }

        var (conn, tx) = transaction.Unwrap();
        return await conn.ExecuteAsync(new CommandDefinition(sql, parameters, transaction: tx, cancellationToken: ct));
    }
}
