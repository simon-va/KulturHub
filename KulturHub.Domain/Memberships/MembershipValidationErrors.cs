using ErrorOr;

namespace KulturHub.Domain.Memberships;

internal static class MembershipValidationErrors
{
    public static readonly Error InvitedAtMustBeUtc =
        Error.Validation("Membership.InvitedAtMustBeUtc", "InvitedAt must be UTC.");

    public static readonly Error DecidedAtMustBeUtc =
        Error.Validation("Membership.DecidedAtMustBeUtc", "DecidedAt must be UTC.");

    public static readonly Error MustBePending =
        Error.Validation("Membership.MustBePending", "Membership must be in status Pending to change its status.");
}