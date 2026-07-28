using ErrorOr;

namespace KulturHub.Application.Errors;

public static class InvitationErrors
{
    public static readonly Error CodeGenerationFailed =
        Error.Conflict("Invitation.CodeGenerationFailed",
            "Could not generate a unique invitation code after multiple attempts.");
}
