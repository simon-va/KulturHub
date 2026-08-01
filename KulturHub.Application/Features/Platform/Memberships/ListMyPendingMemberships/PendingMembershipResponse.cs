namespace KulturHub.Application.Features.Platform.Memberships.ListMyPendingMemberships;

public sealed record PendingMembershipResponse(
    Guid MembershipId,
    Guid OrganisationId,
    string OrganisationName);