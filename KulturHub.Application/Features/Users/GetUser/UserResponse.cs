namespace KulturHub.Application.Features.Users.GetUser;

public record UserResponse(Guid UserId, string FirstName, string LastName, bool IsAdmin);
