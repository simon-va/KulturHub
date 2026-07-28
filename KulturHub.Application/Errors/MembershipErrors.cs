using ErrorOr;

namespace KulturHub.Application.Errors;

public static class MembershipErrors
{
    public static readonly Error UserNotFoundByEmail =
        Error.NotFound("Membership.UserNotFoundByEmail",
            "No user with this email exists.");

    public static readonly Error AlreadyExists =
        Error.Conflict("Membership.AlreadyExists",
            "A membership for this user and organisation already exists.");

    public static readonly Error NotFound =
        Error.NotFound("Membership.NotFound",
            "No membership with this id exists.");

    public static readonly Error MustBePending =
        Error.Validation("Membership.MustBePending",
            "Membership must be in status Pending to change its status.");

    public static readonly Error Forbidden =
        Error.Forbidden("Membership.Forbidden",
            "You are not allowed to change the status of this membership.");
}