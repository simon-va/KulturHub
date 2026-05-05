using ErrorOr;

namespace KulturHub.Application.Errors;

public static class UserErrors
{
    public static Error NotFound(Guid id) =>
        Error.NotFound("User.NotFound", $"User with id '{id}' was not found.");
}
