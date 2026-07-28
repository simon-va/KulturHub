using KulturHub.Domain.Memberships;

namespace KulturHub.Application.Features.Platform.Memberships.InviteMembership;

public sealed record InviteMembershipResponse(
    Guid Id,
    Guid UserId,
    string FullName,
    string Email,
    DateTime JoinedAt,
    MembershipStatus Status);