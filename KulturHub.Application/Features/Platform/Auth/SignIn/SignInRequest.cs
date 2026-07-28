namespace KulturHub.Application.Features.Platform.Auth.SignIn;

public sealed record SignInRequest(
    string Email,
    string Password);
