using ErrorOr;

namespace KulturHub.Application.Features.Invitations.CreateInvitation;

public interface ICreateInvitationService
{
    Task<ErrorOr<CreateInvitationResponse>> CreateAsync(CreateInvitationInput input);
}
