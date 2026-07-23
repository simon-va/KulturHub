namespace KulturHub.Application.Features.Memberships.DeleteOrganisationMembership;

public sealed record DeleteOrganisationMembershipInput(
    Guid OrganisationId,
    Guid MembershipId,
    Guid ActingUserId);
