using ErrorOr;
using FluentValidation;
using KulturHub.Application.Errors;
using KulturHub.Domain.Entities;
using KulturHub.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace KulturHub.Application.Features.Auth.SignUp;

public class AuthService(
    Supabase.Client supabaseClient,
    IAuthRepository authRepository,
    ISupabaseAdminClient supabaseAdminClient,
    IValidator<SignUpInput> validator,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task<ErrorOr<AuthResponse>> SignUpAsync(SignUpInput input)
    {
        var validationResult = await validator.ValidateAsync(input);
        if (!validationResult.IsValid)
            throw new ValidationException(validationResult.Errors);

        Supabase.Gotrue.Session? session;
        try
        {
            session = await supabaseClient.Auth.SignUp(input.Email, input.Password);
        }
        catch (Exception ex) when (ex.Message.Contains("already registered", StringComparison.OrdinalIgnoreCase))
        {
            return AuthErrors.AlreadyRegistered;
        }

        if (session?.User?.Id is null || session.AccessToken is null || session.RefreshToken is null)
            return AuthErrors.SignUpFailed;

        var userId = Guid.Parse(session.User.Id);
        var user = User.Create(userId, input.FirstName, input.LastName);

        try
        {
            await authRepository.InsertUserAsync(user);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to insert user {UserId}. Rolling back auth user.", userId);
            await supabaseAdminClient.DeleteUserAsync(userId);
            return AuthErrors.DatabaseInsertFailed(ex.Message);
        }

        return new AuthResponse(session.AccessToken, session.RefreshToken, userId, input.FirstName, input.LastName);
    }
}
