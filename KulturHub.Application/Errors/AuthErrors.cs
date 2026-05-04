using ErrorOr;

namespace KulturHub.Application.Errors;

public static class AuthErrors
{
    public static readonly Error AlreadyRegistered =
        Error.Conflict("Auth.AlreadyRegistered", "A user with this email address is already registered.");

    public static readonly Error SignUpFailed =
        Error.Failure("Auth.SignUpFailed", "Sign-up failed: the authentication provider did not return a session.");

    public static Error DatabaseInsertFailed(string details) =>
        Error.Failure("Auth.DatabaseInsertFailed", $"Failed to save user profile: {details}");
}
