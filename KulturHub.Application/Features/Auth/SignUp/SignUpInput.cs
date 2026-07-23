namespace KulturHub.Application.Features.Auth.SignUp;

public sealed record SignUpInput(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    string InvitationCode);
