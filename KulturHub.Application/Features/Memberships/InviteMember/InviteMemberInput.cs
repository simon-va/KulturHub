namespace KulturHub.Application.Features.Memberships.InviteMember;

public sealed record InviteMemberInput(
    Guid OrganisationId,
    string Email,
    Guid ActingUserId);
