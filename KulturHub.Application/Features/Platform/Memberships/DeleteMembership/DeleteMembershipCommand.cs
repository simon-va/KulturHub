namespace KulturHub.Application.Features.Platform.Memberships.DeleteMembership;

public sealed record DeleteMembershipCommand(
    Guid MembershipId,
    Guid ActorUserId,
    Guid OrganisationId);
