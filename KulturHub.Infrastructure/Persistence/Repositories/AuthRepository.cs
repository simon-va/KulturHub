using Dapper;
using KulturHub.Domain.Entities;
using KulturHub.Domain.Interfaces;

namespace KulturHub.Infrastructure.Persistence.Repositories;

public class AuthRepository(IDbConnectionFactory connectionFactory) : IAuthRepository
{
    public async Task InsertUserAsync(User user)
    {
        const string sql = """
            INSERT INTO users (user_id, first_name, last_name)
            VALUES (@UserId, @FirstName, @LastName)
            """;

        using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync();
        await connection.ExecuteAsync(sql, new
        {
            user.UserId,
            user.FirstName,
            user.LastName,
        });
    }
}
