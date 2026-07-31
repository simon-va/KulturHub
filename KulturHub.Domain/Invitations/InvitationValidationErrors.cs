using ErrorOr;

namespace KulturHub.Domain.Invitations;

internal static class InvitationValidationErrors
{
    public static readonly Error CodeRequired =
        Error.Validation("Invitation.CodeRequired", "Code is required.");

    public static readonly Error InvalidFormat =
        Error.Validation("Invitation.InvalidFormat",
            "Code must match the format 'XXXX' using A-Z (without I and O) and 2-9.");

    public static readonly Error CreatedAtMustBeUtc =
        Error.Validation("Invitation.CreatedAtMustBeUtc", "CreatedAt must be UTC.");

    public static readonly Error ExpiresAtMustBeUtc =
        Error.Validation("Invitation.ExpiresAtMustBeUtc", "ExpiresAt must be UTC.");

    public static readonly Error ExpiresAtMustBeAfterCreatedAt =
        Error.Validation("Invitation.ExpiresAtMustBeAfterCreatedAt",
            "ExpiresAt must be strictly after CreatedAt.");
}
