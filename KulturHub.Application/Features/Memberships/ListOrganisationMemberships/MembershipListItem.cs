using KulturHub.Domain.Entities;

namespace KulturHub.Application.Features.Memberships.ListOrganisationMemberships;

public sealed record MembershipListItem(
    Guid MembershipId,
    Guid UserId,
    string? FullName,
    string? Email,
    MembershipStatus Status,
    DateTime JoinedAt);
