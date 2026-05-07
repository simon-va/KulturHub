using Dapper;
using KulturHub.Application.Ports;
using KulturHub.Domain.Entities;
using KulturHub.Domain.Interfaces;

namespace KulturHub.Infrastructure.Persistence.Repositories;

public class InvitationRepository(IDbConnectionFactory connectionFactory) : IInvitationRepository
{
    public async Task<Invitation?> GetByCodeAsync(string code)
    {
        const string sql = """
            SELECT id, code, used_by, created_at, expires_at
            FROM invitations
            WHERE code = @Code
            """;

        using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync();
        var row = await connection.QueryFirstOrDefaultAsync<InvitationRow>(sql, new { Code = code });

        return row is null
            ? null
            : Invitation.Reconstitute(row.Id, row.Code, row.UsedBy, row.CreatedAt, row.ExpiresAt);
    }

    private sealed record InvitationRow(Guid Id, string Code, Guid? UsedBy, DateTime CreatedAt, DateTime ExpiresAt);
}
