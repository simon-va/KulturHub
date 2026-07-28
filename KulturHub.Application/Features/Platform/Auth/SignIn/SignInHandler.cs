using ErrorOr;
using KulturHub.Application.Ports;
using Microsoft.Extensions.Logging;

namespace KulturHub.Application.Features.Platform.Auth.SignIn;

public sealed class SignInHandler(
    IAuthProvider authProvider,
    ILogger<SignInHandler> logger)
{
    public async Task<ErrorOr<SignInResponse>> HandleAsync(
        SignInRequest request,
        CancellationToken cancellationToken)
    {
        var signInResult = await authProvider.SignInAsync(
            request.Email, request.Password, cancellationToken);

        if (signInResult.IsError)
            return signInResult.Errors;

        var session = signInResult.Value;

        logger.LogInformation("User signed in: {UserId}", session.UserId);

        return new SignInResponse(
            session.AccessToken,
            session.RefreshToken,
            session.UserId);
    }
}
