namespace KulturHub.Application.Features.Public.Auth.SignUp;

public sealed record SignUpResponse(
    string AccessToken,
    string RefreshToken,
    Guid UserId,
    string FirstName,
    string LastName);