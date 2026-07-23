using Dapper;
using KulturHub.Application.Ports;
using KulturHub.Domain.Entities;
using KulturHub.Infrastructure.Persistence.Internal;
using KulturHub.Infrastructure.Persistence.Mappings;

namespace KulturHub.Infrastructure.Persistence.Repositories;

public class UserRepository(IDbConnectionFactory connectionFactory) : IUserRepository
{
    public async Task InsertUserAsync(
        User user,
        IUnitOfWorkTransaction? transaction = null,
        CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO users (user_id, email, first_name, last_name, is_admin)
            VALUES (@UserId, @Email, @FirstName, @LastName, @IsAdmin)
            """;

        var parameters = new
        {
            user.UserId,
            user.Email,
            user.FirstName,
            user.LastName,
            user.IsAdmin,
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

    public async Task<User?> GetByIdAsync(Guid userId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT
              user_id,
              email,
              first_name,
              last_name,
              is_admin,
              is_deleted,
              deleted_at
            FROM users
            WHERE user_id = @UserId
              AND is_deleted = FALSE
            """;

        using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync<UserMapper.UserRow>(new CommandDefinition(sql, new { UserId = userId }, cancellationToken: ct));
        return row is null ? null : UserMapper.ToEntity(row);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        const string sql = """
            SELECT
              user_id,
              email,
              first_name,
              last_name,
              is_admin,
              is_deleted,
              deleted_at
            FROM users
            WHERE LOWER(email) = LOWER(@Email)
              AND is_deleted = FALSE
            """;

        using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync<UserMapper.UserRow>(
            new CommandDefinition(sql, new { Email = email }, cancellationToken: ct));
        return row is null ? null : UserMapper.ToEntity(row);
    }

    public async Task<int> DeleteAsync(
        Guid userId,
        IUnitOfWorkTransaction? transaction = null,
        CancellationToken ct = default)
    {
        const string sql = """
            UPDATE users
            SET is_deleted = TRUE,
                deleted_at = NOW()
            WHERE user_id = @UserId
              AND is_deleted = FALSE
            """;

        var parameters = new { UserId = userId };

        if (transaction is null)
        {
            using var connection = connectionFactory.CreateConnection();
            await connection.OpenAsync(ct);
            return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: ct));
        }

        var (conn, tx) = transaction.Unwrap();
        return await conn.ExecuteAsync(new CommandDefinition(sql, parameters, transaction: tx, cancellationToken: ct));
    }

    public async Task<bool> IsAdminAsync(Guid userId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT is_admin
            FROM users
            WHERE user_id = @UserId
              AND is_deleted = FALSE
            """;

        using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(ct);
        var result = await connection.ExecuteScalarAsync<bool?>(new CommandDefinition(sql, new { UserId = userId }, cancellationToken: ct));
        return result ?? false;
    }
}
