using ErrorOr;

namespace KulturHub.Application.Errors;

public static class InvitationErrors
{
    public static readonly Error CodeGenerationFailed =
        Error.Conflict("Invitation.CodeGenerationFailed",
            "Could not generate a unique invitation code after multiple attempts.");

    public static readonly Error NotFound =
        Error.NotFound("Invitation.NotFound", "Invitation code not found.");

    public static readonly Error AlreadyUsed =
        Error.Conflict("Invitation.AlreadyUsed",
            "This invitation code has already been used.");

    public static readonly Error Expired =
        Error.Conflict("Invitation.Expired",
            "This invitation code has expired.");
}