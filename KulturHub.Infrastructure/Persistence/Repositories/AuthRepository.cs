using Dapper;
using KulturHub.Application.Errors;
using KulturHub.Application.Ports;
using KulturHub.Domain.Entities;

namespace KulturHub.Infrastructure.Persistence.Repositories;

public class AuthRepository(IDbConnectionFactory connectionFactory) : IAuthRepository
{
    public async Task<Invitation?> GetInvitationByCodeAsync(string code)
    {
        const string sql = """
            SELECT id, code, used_by AS UsedBy, created_at AS CreatedAt, expires_at AS ExpiresAt
            FROM invitations
            WHERE code = @Code
            """;

        using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync();
        var row = await connection.QuerySingleOrDefaultAsync<InvitationRow>(sql, new { Code = code });
        if (row is null)
            return null;

        var createdAt = DateTime.SpecifyKind(row.CreatedAt, DateTimeKind.Utc);
        var expiresAt = DateTime.SpecifyKind(row.ExpiresAt, DateTimeKind.Utc);
        return Invitation.Reconstitute(row.Id, row.Code, row.UsedBy, createdAt, expiresAt);
    }

    public async Task InsertUserAsync(User user, Guid invitationId)
    {
        const string insertUser = """
            INSERT INTO users (user_id, first_name, last_name)
            VALUES (@UserId, @FirstName, @LastName)
            """;

        const string markInvitation = """
            UPDATE invitations
            SET used_by = @UsedBy
            WHERE id = @InvitationId AND used_by IS NULL
            """;

        using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync();
        using var transaction = await connection.BeginTransactionAsync();

        await connection.ExecuteAsync(insertUser, new
        {
            user.UserId,
            user.FirstName,
            user.LastName,
        }, transaction);

        var rows = await connection.ExecuteAsync(markInvitation, new
        {
            UsedBy = user.UserId,
            InvitationId = invitationId,
        }, transaction);

        if (rows == 0)
        {
            await transaction.RollbackAsync();
            throw new InvitationAlreadyUsedException(invitationId);
        }

        await transaction.CommitAsync();
    }

    private sealed record InvitationRow(Guid Id, string Code, Guid? UsedBy, DateTime CreatedAt, DateTime ExpiresAt);
}
