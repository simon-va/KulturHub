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
}

public sealed class InvitationAlreadyUsedException(Guid invitationId)
    : Exception($"Invitation {invitationId} was already used by a concurrent request.")
{
    public Guid InvitationId { get; } = invitationId;
}
