using ErrorOr;
using FluentValidation;
using KulturHub.Application.Errors;
using KulturHub.Application.Ports;

namespace KulturHub.Application.Features.Auth.SignIn;

public sealed class SignInHandler(
    IAuthProvider authProvider,
    IUserRepository authRepository,
    IValidator<SignInInput> validator)
{
    public async Task<ErrorOr<SignInResponse>> ExecuteAsync(SignInInput input, CancellationToken cancellationToken = default)
    {
        var validationResult = await validator.ValidateAsync(input, cancellationToken);
        if (!validationResult.IsValid)
            return validationResult.Errors
                .Select(e => Error.Validation(e.PropertyName, e.ErrorMessage))
                .ToList();

        var sessionResult = await authProvider.SignInAsync(input.Email, input.Password, cancellationToken);
        if (sessionResult.IsError)
            return sessionResult.Errors;

        var session = sessionResult.Value;

        var user = await authRepository.GetByIdAsync(session.UserId, cancellationToken);
        if (user is null)
            return AuthErrors.NotFound;

        return new SignInResponse(
            session.AccessToken,
            session.RefreshToken,
            session.UserId,
            user.FirstName,
            user.LastName);
    }
}
