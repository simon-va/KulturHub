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
}