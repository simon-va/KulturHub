using ErrorOr;
using KulturHub.Application.Abstractions.Persistence;
using KulturHub.Application.Errors;
using KulturHub.Application.Ports;
using KulturHub.Domain.Invitations;
using KulturHub.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KulturHub.Application.Features.Public.Auth.SignUp;

public sealed class SignUpHandler(
    IAppDbContext db,
    IAuthProvider authProvider,
    IUserAdminClient userAdminClient,
    TimeProvider clock,
    ILogger<SignUpHandler> logger)
{
    public async Task<ErrorOr<SignUpResponse>> HandleAsync(
        SignUpRequest request,
        CancellationToken cancellationToken)
    {
        var invitation = await db.Invitations
            .FirstOrDefaultAsync(i => i.Code == request.InvitationCode, cancellationToken);

        if (invitation is null)
            return InvitationErrors.NotFound;

        if (invitation.IsUsed)
            return InvitationErrors.AlreadyUsed;

        var now = clock.GetUtcNow().UtcDateTime;
        if (invitation.ExpiresAt <= now)
            return InvitationErrors.Expired;

        var signUpResult = await authProvider.SignUpAsync(
            request.Email, request.Password, cancellationToken);

        if (signUpResult.IsError)
            return signUpResult.Errors;

        var session = signUpResult.Value;

        var userResult = User.Create(
            UserId.From(session.UserId),
            request.Email,
            request.FirstName,
            request.LastName,
            clock,
            isAdmin: false);

        if (userResult.IsError)
        {
            await TryRollbackAuthUserAsync(session.UserId, cancellationToken);
            return userResult.Errors;
        }

        var user = userResult.Value;
        invitation.MarkAsUsed(user.Id.Value);

        db.Users.Add(user);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex,
                "Failed to persist user profile during sign-up; rolling back auth account.");

            var rolledBack = await TryRollbackAuthUserAsync(session.UserId, cancellationToken);
            return rolledBack
                ? AuthErrors.UserCreationRolledBack
                : AuthErrors.CompensatingDeleteFailed;
        }

        logger.LogInformation("User signed up: {UserId}", user.Id.Value);

        return new SignUpResponse(
            session.AccessToken,
            session.RefreshToken,
            user.Id.Value,
            user.FirstName,
            user.LastName);
    }

    private async Task<bool> TryRollbackAuthUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            var deleted = await userAdminClient.DeleteUserAsync(userId, cancellationToken);
            if (!deleted)
            {
                logger.LogError(
                    "Compensating delete of auth user returned a non-success status: {UserId}",
                    userId);
            }
            return deleted;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Compensating delete of auth user threw an exception: {UserId}",
                userId);
            return false;
        }
    }
}