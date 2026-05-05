namespace KulturHub.Api.Requests;

public record SignUpRequest(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    string InvitationCode);
