namespace KulturHub.Application.Features.Admin.Invitations.CreateInvitation;

public sealed record CreateInvitationResponse(
    Guid Id,
    string Code,
    DateTime CreatedAt,
    DateTime ExpiresAt);
