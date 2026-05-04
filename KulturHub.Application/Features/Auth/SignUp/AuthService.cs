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

        var sessionResult = await authProvider.SignUpAsync(input.Email, input.Password);
        if (sessionResult.IsError)
            return sessionResult.Errors;

        var session = sessionResult.Value;
        var user = User.Create(session.UserId, input.FirstName, input.LastName);

        try
        {
            await authRepository.InsertUserAsync(user);
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
