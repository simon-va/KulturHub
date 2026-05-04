using Dapper;
using KulturHub.Domain.Entities;
using KulturHub.Domain.Interfaces;
using KulturHub.Infrastructure.Persistence;

namespace KulturHub.Infrastructure.Persistence.Repositories;

public class InstagramTokenRepository(IDbConnectionFactory connectionFactory) : IInstagramTokenRepository
{
    public async Task<InstagramToken?> GetCurrentTokenAsync()
    {
        const string sql = """
            SELECT id, access_token AS AccessToken, instagram_user_id AS InstagramUserId,
                   expires_at AS ExpiresAt, last_refreshed_at AS LastRefreshedAt, created_at AS CreatedAt
            FROM instagram_tokens
            ORDER BY created_at DESC
            LIMIT 1
            """;

        using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<InstagramToken>(sql);
    }

    public async Task UpdateTokenAsync(InstagramToken token)
    {
        const string sql = """
            UPDATE instagram_tokens
            SET access_token = @AccessToken,
                expires_at = @ExpiresAt,
                last_refreshed_at = @LastRefreshedAt
            WHERE id = @Id
            """;

        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(sql, new
        {
            token.Id,
            token.AccessToken,
            token.ExpiresAt,
            token.LastRefreshedAt
        });
    }

    public async Task CreateTokenAsync(InstagramToken token)
    {
        const string sql = """
            INSERT INTO instagram_tokens (id, access_token, instagram_user_id, expires_at, last_refreshed_at, created_at)
            VALUES (@Id, @AccessToken, @InstagramUserId, @ExpiresAt, @LastRefreshedAt, @CreatedAt)
            """;

        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(sql, new
        {
            token.Id,
            token.AccessToken,
            token.InstagramUserId,
            token.ExpiresAt,
            token.LastRefreshedAt,
            token.CreatedAt
        });
    }
}
