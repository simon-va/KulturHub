using ErrorOr;
using FluentValidation;
using KulturHub.Application.Errors;
using KulturHub.Application.Ports;
using KulturHub.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace KulturHub.Application.Features.Auth.SignUp;

public sealed class SignUpHandler(
    IAuthProvider authProvider,
    IUserRepository authRepository,
    IInvitationRepository invitationRepository,
    IUserAdminClient userAdminClient,
    IValidator<SignUpInput> validator,
    ILogger<SignUpHandler> logger)
{
    public async Task<ErrorOr<SignUpResponse>> ExecuteAsync(SignUpInput input, CancellationToken cancellationToken = default)
    {
        var validationResult = await validator.ValidateAsync(input, cancellationToken);
        if (!validationResult.IsValid)
            return validationResult.Errors
                .Select(e => Error.Validation(e.PropertyName, e.ErrorMessage))
                .ToList();

        var invitation = await invitationRepository.GetByCodeAsync(input.InvitationCode, cancellationToken);
        if (invitation is null)
            return InvitationErrors.NotFound;

        switch (invitation.EnsureCanBeUsed())
        {
            case InvitationValidation.Expired:     return InvitationErrors.Expired;
            case InvitationValidation.AlreadyUsed: return InvitationErrors.AlreadyUsed;
        }

        var sessionResult = await authProvider.SignUpAsync(input.Email, input.Password, cancellationToken);
        if (sessionResult.IsError)
            return sessionResult.Errors;

        var session = sessionResult.Value;
        var user = User.Create(session.UserId, input.Email, input.FirstName, input.LastName);

        try
        {
            await authRepository.InsertUserAsync(user, null, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to insert user {UserId}. Rolling back auth user.", session.UserId);
            await userAdminClient.DeleteUserAsync(session.UserId, cancellationToken);
            return AuthErrors.DatabaseInsertFailed(ex.Message);
        }

        try
        {
            var markResult = await invitationRepository.MarkAsUsedAsync(invitation.Id, session.UserId, null, cancellationToken);
            if (markResult.IsError)
            {
                logger.LogWarning("Invitation {InvitationId} was claimed concurrently. Rolling back auth user {UserId}.",
                    invitation.Id, session.UserId);
                await userAdminClient.DeleteUserAsync(session.UserId, cancellationToken);
                return markResult.Errors;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to mark invitation {InvitationId} as used. Rolling back auth user {UserId}.",
                invitation.Id, session.UserId);
            await userAdminClient.DeleteUserAsync(session.UserId, cancellationToken);
            return AuthErrors.DatabaseInsertFailed(ex.Message);
        }

        return new SignUpResponse(session.AccessToken, session.RefreshToken, session.UserId, input.FirstName, input.LastName);
    }
}
