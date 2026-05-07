namespace KulturHub.Application.Features.Invitations.CreateInvitation;

public record CreateInvitationResponse(Guid Id, string Code, DateTime ExpiresAt, DateTime CreatedAt);
