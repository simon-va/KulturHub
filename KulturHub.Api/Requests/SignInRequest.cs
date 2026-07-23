namespace KulturHub.Api.Requests;

public sealed record SignInRequest(
    string Email,
    string Password);
