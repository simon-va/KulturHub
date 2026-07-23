using KulturHub.Domain.Entities;

namespace KulturHub.Application.Features.Memberships.InviteMember;

public sealed record InvitedMember(
    Guid MembershipId,
    Guid UserId,
    string? Email,
    MembershipStatus Status);
