using Dapper;
using KulturHub.Application.Features.Memberships.ListMyPendingMemberships;
using KulturHub.Application.Features.Memberships.ListOrganisationMemberships;
using KulturHub.Application.Ports;
using KulturHub.Domain.Entities;
using KulturHub.Infrastructure.Persistence.Internal;
using KulturHub.Infrastructure.Persistence.Mappings;
using KulturHub.Infrastructure.Persistence.Models;

namespace KulturHub.Infrastructure.Persistence.Repositories;

public class MembershipRepository(IDbConnectionFactory connectionFactory) : IMembershipRepository
{
    public async Task InsertAsync(
        Membership membership,
        IUnitOfWorkTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO memberships (id, user_id, organisation_id, joined_at, status, invited_by)
            VALUES (@Id, @UserId, @OrganisationId, @JoinedAt, @Status, @InvitedBy)
            """;

        var parameters = new
        {
            membership.Id,
            membership.UserId,
            membership.OrganisationId,
            membership.JoinedAt,
            Status = (short)membership.Status,
            membership.InvitedBy,
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

    public async Task<bool> IsMemberAsync(
        Guid userId,
        Guid organisationId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM memberships m
                INNER JOIN organisations o ON o.id = m.organisation_id
                WHERE m.user_id = @UserId
                  AND m.organisation_id = @OrganisationId
                  AND m.is_deleted = FALSE
                  AND o.is_deleted = FALSE
            )
            """;

        using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            sql,
            new { UserId = userId, OrganisationId = organisationId },
            cancellationToken: cancellationToken));
    }

    public async Task<Membership?> GetByIdAsync(
        Guid id,
        IUnitOfWorkTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                id,
                user_id,
                organisation_id,
                joined_at,
                is_deleted,
                deleted_at,
                status,
                invited_by
            FROM memberships
            WHERE id = @Id
            """;

        var parameters = new { Id = id };

        if (transaction is null)
        {
            using var connection = connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            var row = await connection.QuerySingleOrDefaultAsync<MembershipMapper.MembershipRow>(
                new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
            return row is null ? null : MembershipMapper.ToEntity(row);
        }

        var (conn, tx) = transaction.Unwrap();
        var row2 = await conn.QuerySingleOrDefaultAsync<MembershipMapper.MembershipRow>(
            new CommandDefinition(sql, parameters, transaction: tx, cancellationToken: cancellationToken));
        return row2 is null ? null : MembershipMapper.ToEntity(row2);
    }

    public async Task<int> CountActiveByOrganisationAsync(
        Guid organisationId,
        IUnitOfWorkTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        if (transaction is null)
        {
            const string sqlWithoutLock = """
                SELECT COUNT(*)::int
                FROM memberships
                WHERE organisation_id = @OrganisationId
                  AND is_deleted = FALSE
                """;

            using var connection = connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                sqlWithoutLock,
                new { OrganisationId = organisationId },
                cancellationToken: cancellationToken));
        }

        const string sqlWithLock = """
            SELECT COUNT(*)::int
            FROM memberships
            WHERE organisation_id = @OrganisationId
              AND is_deleted = FALSE
            FOR UPDATE
            """;

        var (conn, tx) = transaction.Unwrap();
        return await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            sqlWithLock,
            new { OrganisationId = organisationId },
            transaction: tx,
            cancellationToken: cancellationToken));
    }

    public async Task<int> SoftDeleteAsync(
        Guid id,
        IUnitOfWorkTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE memberships
            SET is_deleted = TRUE,
                deleted_at = NOW()
            WHERE id = @Id
              AND is_deleted = FALSE
            """;

        if (transaction is null)
        {
            using var connection = connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            return await connection.ExecuteAsync(new CommandDefinition(
                sql,
                new { Id = id },
                cancellationToken: cancellationToken));
        }

        var (conn, tx) = transaction.Unwrap();
        return await conn.ExecuteAsync(new CommandDefinition(
            sql,
            new { Id = id },
            transaction: tx,
            cancellationToken: cancellationToken));
    }

    public async Task<int> SoftDeleteByOrganisationAsync(
        Guid organisationId,
        IUnitOfWorkTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE memberships
            SET is_deleted = TRUE,
                deleted_at = NOW()
            WHERE organisation_id = @OrganisationId
              AND is_deleted = FALSE
            """;

        if (transaction is null)
        {
            using var connection = connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            return await connection.ExecuteAsync(new CommandDefinition(sql, new { OrganisationId = organisationId }, cancellationToken: cancellationToken));
        }

        var (conn, tx) = transaction.Unwrap();
        return await conn.ExecuteAsync(new CommandDefinition(sql, new { OrganisationId = organisationId }, transaction: tx, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<Guid>> GetActiveOrganisationIdsWhereUserIsSoleMemberAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT m.organisation_id
            FROM memberships m
            WHERE m.is_deleted = FALSE
              AND m.user_id = @UserId
              AND NOT EXISTS (
                  SELECT 1
                  FROM memberships other
                  WHERE other.organisation_id = m.organisation_id
                    AND other.is_deleted = FALSE
                    AND other.user_id <> @UserId
              )
            """;

        using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<Guid>(new CommandDefinition(
            sql,
            new { UserId = userId },
            cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<MembershipListItem>> ListActiveByOrganisationIdAsync(
        Guid organisationId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                m.id            AS MembershipId,
                m.user_id       AS UserId,
                u.first_name    AS FirstName,
                u.last_name     AS LastName,
                u.email         AS Email,
                m.status        AS Status,
                m.joined_at     AS JoinedAt
            FROM memberships m
            INNER JOIN users u ON u.user_id = m.user_id AND u.is_deleted = FALSE
            INNER JOIN organisations o ON o.id = m.organisation_id AND o.is_deleted = FALSE
            WHERE m.organisation_id = @OrganisationId
              AND m.is_deleted = FALSE
            ORDER BY u.last_name ASC NULLS LAST,
                     u.first_name ASC NULLS LAST,
                     m.id ASC
            """;

        using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<MembershipReadRow>(
            new CommandDefinition(
                sql,
                new { OrganisationId = organisationId },
                cancellationToken: cancellationToken));

        return rows.Select(MembershipReadMapper.ToListItem).ToList();
    }

    public async Task<Membership?> GetActiveByUserAndOrganisationAsync(
        Guid userId,
        Guid organisationId,
        IUnitOfWorkTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                id,
                user_id,
                organisation_id,
                joined_at,
                is_deleted,
                deleted_at,
                status,
                invited_by
            FROM memberships
            WHERE user_id = @UserId
              AND organisation_id = @OrganisationId
              AND is_deleted = FALSE
            """;

        var parameters = new { UserId = userId, OrganisationId = organisationId };

        if (transaction is null)
        {
            using var connection = connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            var row = await connection.QuerySingleOrDefaultAsync<MembershipMapper.MembershipRow>(
                new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
            return row is null ? null : MembershipMapper.ToEntity(row);
        }

        var (conn, tx) = transaction.Unwrap();
        var row2 = await conn.QuerySingleOrDefaultAsync<MembershipMapper.MembershipRow>(
            new CommandDefinition(sql, parameters, transaction: tx, cancellationToken: cancellationToken));
        return row2 is null ? null : MembershipMapper.ToEntity(row2);
    }

    public async Task<int> UpdateStatusAsync(
        Guid id,
        MembershipStatus status,
        IUnitOfWorkTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE memberships
            SET status = @Status
            WHERE id = @Id
              AND is_deleted = FALSE
              AND status = 0
            """;

        var parameters = new
        {
            Id = id,
            Status = (short)status,
        };

        if (transaction is null)
        {
            using var connection = connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        }

        var (conn, tx) = transaction.Unwrap();
        return await conn.ExecuteAsync(new CommandDefinition(sql, parameters, transaction: tx, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<PendingMembershipListItem>> ListPendingByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                m.id            AS MembershipId,
                inv.first_name  AS InviterFirstName,
                inv.last_name   AS InviterLastName,
                o.name          AS OrganisationName,
                m.joined_at     AS JoinedAt
            FROM memberships m
            INNER JOIN organisations o ON o.id = m.organisation_id AND o.is_deleted = FALSE
            LEFT JOIN users inv        ON inv.user_id = m.invited_by AND inv.is_deleted = FALSE
            WHERE m.user_id = @UserId
              AND m.is_deleted = FALSE
              AND m.status = 0
            ORDER BY m.joined_at DESC, m.id ASC
            """;

        using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<PendingMembershipReadRow>(
            new CommandDefinition(
                sql,
                new { UserId = userId },
                cancellationToken: cancellationToken));

        return rows.Select(PendingMembershipReadMapper.ToListItem).ToList();
    }
}
