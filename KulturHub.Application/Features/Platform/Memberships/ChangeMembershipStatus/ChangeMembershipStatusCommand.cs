namespace KulturHub.Application.Features.Platform.Memberships.ChangeMembershipStatus;

public sealed record ChangeMembershipStatusCommand(
    Guid MembershipId,
    Guid CallerUserId,
    MembershipChangeStatus NewStatus);