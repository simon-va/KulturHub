using KulturHub.Domain.Entities;

namespace KulturHub.Application.Features.Invitations.CreateInvitation;

public sealed record CreateInvitationResponse(
    Guid Id,
    string Code,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    bool IsUsed,
    bool IsExpired)
{
    public static CreateInvitationResponse From(Invitation invitation) => new(
        invitation.Id,
        invitation.Code,
        invitation.CreatedAt,
        invitation.ExpiresAt,
        invitation.IsUsed,
        invitation.IsExpired);
}
