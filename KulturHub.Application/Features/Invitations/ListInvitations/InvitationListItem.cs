namespace KulturHub.Application.Features.Invitations.ListInvitations;

public sealed record InvitationListItem(
    Guid Id,
    string Code,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    bool IsUsed,
    bool IsExpired,
    Guid? UsedById,
    string? UsedByFullName);
