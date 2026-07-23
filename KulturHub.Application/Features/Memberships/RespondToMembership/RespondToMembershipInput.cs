namespace KulturHub.Application.Features.Memberships.RespondToMembership;

public sealed record RespondToMembershipInput(
    Guid MembershipId,
    Guid ActingUserId,
    MembershipDecision Decision);
