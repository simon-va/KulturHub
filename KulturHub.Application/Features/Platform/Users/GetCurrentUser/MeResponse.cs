namespace KulturHub.Application.Features.Platform.Users.GetCurrentUser;

public sealed record MeResponse(
    Guid UserId,
    string FirstName,
    string LastName,
    string Email,
    DateTime CreatedAt);
