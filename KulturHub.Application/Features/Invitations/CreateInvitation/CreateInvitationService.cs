using ErrorOr;
using KulturHub.Application.Errors;
using KulturHub.Domain.Entities;
using KulturHub.Domain.Interfaces;

namespace KulturHub.Application.Features.Invitations.CreateInvitation;

public class CreateInvitationService(
    IUserRepository userRepository,
    IInvitationRepository invitationRepository) : ICreateInvitationService
{
    public async Task<ErrorOr<CreateInvitationResponse>> CreateAsync(CreateInvitationInput input)
    {
        var user = await userRepository.GetByIdAsync(input.UserId);
        if (user is null)
            return UserErrors.NotFound(input.UserId);

        if (!user.IsAdmin)
            return UserErrors.NotAdmin;

        var invitation = Invitation.Create();
        await invitationRepository.CreateAsync(invitation);

        return new CreateInvitationResponse(invitation.Id, invitation.Code, invitation.ExpiresAt, invitation.CreatedAt);
    }
}
