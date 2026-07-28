using ErrorOr;

namespace KulturHub.Application.Errors;

public static class AuthErrors
{
    public static readonly Error AlreadyRegistered =
        Error.Conflict("Auth.AlreadyRegistered", "A user with this email address is already registered.");

    public static readonly Error SignUpFailed =
        Error.Failure("Auth.SignUpFailed", "Sign-up failed: the authentication provider did not return a session.");

    public static readonly Error InvalidCredentials =
        Error.Unauthorized("Auth.InvalidCredentials", "Invalid email or password.");

    public static readonly Error SignInFailed =
        Error.Failure("Auth.SignInFailed", "Sign-in failed: the authentication provider did not return a session.");

    public static readonly Error NotFound =
        Error.NotFound("Auth.NotFound", "User not found.");

    public static readonly Error DeleteProviderFailed =
        Error.Failure("Auth.DeleteProviderFailed", "Failed to delete the user from the authentication provider.");

    public static readonly Error UserCreationRolledBack =
        Error.Failure("Auth.UserCreationRolledBack",
            "Sign-up failed: the local user profile could not be saved. The authentication account has been rolled back.");

    public static readonly Error CompensatingDeleteFailed =
        Error.Failure("Auth.CompensatingDeleteFailed",
            "Sign-up failed: the local user profile could not be saved AND the authentication account could not be rolled back. Manual cleanup is required.");

    public static Error SoleMemberOfOrganisations(IReadOnlyCollection<Guid> organisationIds) =>
        Error.Conflict(
            "Auth.SoleMemberOfOrganisations",
            "Account cannot be deleted because you are the only active member of the following organisation(s): "
                + string.Join(", ", organisationIds));

    public static Error DatabaseInsertFailed(string details) =>
        Error.Failure("Auth.DatabaseInsertFailed", $"Failed to save user profile: {details}");
}