using ErrorOr;

namespace KulturHub.Domain.Memberships;

internal static class MembershipValidationErrors
{
    public static readonly Error JoinedAtMustBeUtc =
        Error.Validation("Membership.JoinedAtMustBeUtc", "JoinedAt must be UTC.");
}
