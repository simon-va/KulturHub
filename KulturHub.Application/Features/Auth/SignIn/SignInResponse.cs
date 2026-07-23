namespace KulturHub.Application.Features.Auth.SignIn;

public sealed record SignInResponse(
    string AccessToken,
    string RefreshToken,
    Guid UserId,
    string FirstName,
    string LastName);
