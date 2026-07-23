namespace KulturHub.Api.Requests;

public sealed record SignUpRequest(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    string InvitationCode);
