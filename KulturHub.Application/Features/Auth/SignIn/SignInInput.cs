namespace KulturHub.Application.Features.Auth.SignIn;

public sealed record SignInInput(
    string Email,
    string Password);
