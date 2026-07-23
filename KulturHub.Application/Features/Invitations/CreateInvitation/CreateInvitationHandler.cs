using ErrorOr;
using KulturHub.Application.Errors;
using KulturHub.Application.Ports;
using KulturHub.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace KulturHub.Application.Features.Invitations.CreateInvitation;

public sealed class CreateInvitationHandler(
    IInvitationRepository invitationRepository,
    ILogger<CreateInvitationHandler> logger)
{
    public async Task<ErrorOr<CreateInvitationResponse>> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        var invitation = Invitation.Create();

        try
        {
            await invitationRepository.InsertAsync(invitation, null, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to insert invitation {InvitationId}.", invitation.Id);
            return InvitationErrors.CreateFailed(ex.Message);
        }

        return CreateInvitationResponse.From(invitation);
    }
}
