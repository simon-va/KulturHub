namespace KulturHub.Application.Features.Memberships.ListMyPendingMemberships;

public sealed record PendingMembershipListItem(
    Guid MembershipId,
    string? InviterName,
    string OrganisationName);
