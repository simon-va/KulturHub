using Dapper;
using KulturHub.Application.Ports;
using KulturHub.Domain.Entities;
using KulturHub.Domain.Interfaces;

namespace KulturHub.Infrastructure.Persistence.Repositories;

public class UserRepository(IDbConnectionFactory connectionFactory) : IUserRepository
{
    public async Task<User?> GetByIdAsync(Guid userId)
    {
        const string sql = """
            SELECT user_id, first_name, last_name, is_admin
            FROM users
            WHERE user_id = @UserId
            """;

        using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync();
        var row = await connection.QuerySingleOrDefaultAsync<UserRow>(sql, new { UserId = userId });
        if (row is null)
            return null;

        return User.Create(row.user_id, row.first_name, row.last_name, row.is_admin);
    }

    private sealed record UserRow(Guid user_id, string first_name, string last_name, bool is_admin);
}
