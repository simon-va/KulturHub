namespace KulturHub.Application.Features.Auth.SignUp;

public sealed record SignUpResponse(
    string AccessToken,
    string RefreshToken,
    Guid UserId,
    string FirstName,
    string LastName);
