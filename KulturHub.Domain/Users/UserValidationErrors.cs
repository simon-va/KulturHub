using ErrorOr;

namespace KulturHub.Domain.Users;

internal static class UserValidationErrors
{
    public static readonly Error EmailRequired =
        Error.Validation("User.EmailRequired", "Email is required.");

    public static readonly Error EmailInvalid =
        Error.Validation("User.EmailInvalid", "Email address is not valid.");

    public static readonly Error FirstNameRequired =
        Error.Validation("User.FirstNameRequired", "First name is required.");

    public static readonly Error FirstNameTooLong =
        Error.Validation("User.FirstNameTooLong", "First name must not exceed 100 characters.");

    public static readonly Error LastNameRequired =
        Error.Validation("User.LastNameRequired", "Last name is required.");

    public static readonly Error LastNameTooLong =
        Error.Validation("User.LastNameTooLong", "Last name must not exceed 100 characters.");

    public static readonly Error CreatedAtMustBeUtc =
        Error.Validation("User.CreatedAtMustBeUtc", "CreatedAt must be UTC.");

    public static readonly Error DeletedAtMustBeUtc =
        Error.Validation("User.DeletedAtMustBeUtc", "DeletedAt must be UTC.");
}