using ErrorOr;
using KulturHub.Application.Errors;
using KulturHub.Application.Ports;
using Microsoft.Extensions.Logging;

namespace KulturHub.Application.Features.Invitations.DeleteInvitation;

public sealed class DeleteInvitationHandler(
    IInvitationRepository invitationRepository,
    ILogger<DeleteInvitationHandler> logger)
{
    public async Task<ErrorOr<Deleted>> ExecuteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var invitation = await invitationRepository.GetByIdAsync(id, cancellationToken);
        if (invitation is null)
            return InvitationErrors.NotFound;

        if (invitation.IsUsed)
            return InvitationErrors.DeleteAlreadyUsed;

        invitation.MarkAsDeleted();
        var rows = await invitationRepository.DeleteAsync(id, null, cancellationToken);
        if (rows == 0)
        {
            logger.LogWarning("Invitation {InvitationId} could not be deleted (already used concurrently).", id);
            return InvitationErrors.DeleteAlreadyUsed;
        }

        return Result.Deleted;
    }
}
