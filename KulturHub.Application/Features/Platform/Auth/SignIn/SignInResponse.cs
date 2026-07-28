namespace KulturHub.Application.Features.Platform.Auth.SignIn;

public sealed record SignInResponse(
    string AccessToken,
    string RefreshToken,
    Guid UserId);
