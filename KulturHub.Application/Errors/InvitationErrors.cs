using ErrorOr;

namespace KulturHub.Application.Errors;

public static class InvitationErrors
{
    public static readonly Error NotFound =
        Error.NotFound("Invitation.NotFound", "Invitation code not found.");

    public static readonly Error Expired =
        Error.Validation("Invitation.Expired", "Invitation code has expired.");

    public static readonly Error AlreadyUsed =
        Error.Conflict("Invitation.AlreadyUsed", "Invitation code has already been used.");

    public static readonly Error DeleteAlreadyUsed =
        Error.Conflict("Invitation.DeleteAlreadyUsed", "Cannot delete an invitation that has already been used.");

    public static Error CreateFailed(string details) =>
        Error.Failure("Invitation.CreateFailed", $"Failed to create invitation: {details}");
}
