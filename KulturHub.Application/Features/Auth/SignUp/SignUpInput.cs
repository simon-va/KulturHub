namespace KulturHub.Application.Features.Auth.SignUp;

public record SignUpInput(
    string FirstName,
    string LastName,
    string Email,
    string Password);
