using ErrorOr;
using KulturHub.Application.Errors;
using KulturHub.Application.Ports;
using Microsoft.Extensions.Logging;

namespace KulturHub.Application.Features.Auth.DeleteAccount;

public sealed class DeleteAccountHandler(
    IUserRepository userRepository,
    IMembershipRepository membershipRepository,
    IUserAdminClient userAdminClient,
    ILogger<DeleteAccountHandler> logger)
{
    public async Task<ErrorOr<Deleted>> ExecuteAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
            return AuthErrors.NotFound;

        var soleMemberOrganisationIds =
            await membershipRepository.GetActiveOrganisationIdsWhereUserIsSoleMemberAsync(userId, cancellationToken);
        if (soleMemberOrganisationIds.Count > 0)
        {
            logger.LogInformation(
                "User {UserId} cannot be deleted because they are the sole active member of {Count} organisation(s).",
                userId, soleMemberOrganisationIds.Count);
            return AuthErrors.SoleMemberOfOrganisations(soleMemberOrganisationIds);
        }

        var providerDeleted = await userAdminClient.DeleteUserAsync(userId, cancellationToken);
        if (!providerDeleted)
        {
            logger.LogError("Supabase delete failed for user {UserId}. Database row left unchanged.", userId);
            return AuthErrors.DeleteProviderFailed;
        }

        user.MarkAsDeleted();
        var rows = await userRepository.DeleteAsync(userId, null, cancellationToken);
        if (rows == 0)
        {
            logger.LogWarning("User {UserId} deleted from Supabase but no database row was removed.", userId);
        }

        return Result.Deleted;
    }
}
