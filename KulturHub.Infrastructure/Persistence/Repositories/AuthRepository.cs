using Dapper;
using KulturHub.Application.Ports;
using KulturHub.Domain.Entities;
using KulturHub.Domain.Interfaces;

namespace KulturHub.Infrastructure.Persistence.Repositories;

public class AuthRepository(IDbConnectionFactory connectionFactory) : IAuthRepository
{
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
            throw new InvalidOperationException($"Invitation {invitationId} was already used by a concurrent request.");

        await transaction.CommitAsync();
    }
}
