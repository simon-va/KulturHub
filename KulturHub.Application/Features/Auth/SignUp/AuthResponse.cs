namespace KulturHub.Application.Features.Auth.SignUp;

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    Guid UserId,
    string FirstName,
    string LastName);
