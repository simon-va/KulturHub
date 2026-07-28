namespace KulturHub.Application.Features.Public.Auth.SignUp;

public sealed record SignUpRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string InvitationCode);