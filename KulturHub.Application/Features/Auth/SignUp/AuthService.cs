using ErrorOr;
using FluentValidation;
using KulturHub.Application.Errors;
using KulturHub.Application.Ports;
using KulturHub.Domain.Entities;
using KulturHub.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace KulturHub.Application.Features.Auth.SignUp;

public class AuthService(
    IAuthProvider authProvider,
    IAuthRepository authRepository,
    IInvitationRepository invitationRepository,
    ISupabaseAdminClient supabaseAdminClient,
    IValidator<SignUpInput> validator,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task<ErrorOr<AuthResponse>> SignUpAsync(SignUpInput input)
    {
        var validationResult = await validator.ValidateAsync(input);
        if (!validationResult.IsValid)
            return validationResult.Errors
                .Select(e => Error.Validation(e.PropertyName, e.ErrorMessage))
                .ToList();

        var invitation = await invitationRepository.GetByCodeAsync(input.InvitationCode);
        if (invitation is null)
            return InvitationErrors.NotFound;
        if (invitation.IsExpired)
            return InvitationErrors.Expired;
        if (invitation.IsUsed)
            return InvitationErrors.AlreadyUsed;

        var sessionResult = await authProvider.SignUpAsync(input.Email, input.Password);
        if (sessionResult.IsError)
            return sessionResult.Errors;

        var session = sessionResult.Value;
        var user = User.Create(session.UserId, input.FirstName, input.LastName);

        try
        {
            await authRepository.InsertUserAsync(user, invitation.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to insert user {UserId}. Rolling back auth user.", session.UserId);
            await supabaseAdminClient.DeleteUserAsync(session.UserId);
            return AuthErrors.DatabaseInsertFailed(ex.Message);
        }

        return new AuthResponse(session.AccessToken, session.RefreshToken, session.UserId, input.FirstName, input.LastName);
    }
}
