namespace KulturHub.Application.Features.Platform.Memberships.InviteMembership;

public sealed record InviteMembershipCommand(
    Guid OrganisationId,
    Guid InviterUserId,
    string Email);