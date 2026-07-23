using KulturHub.Application.Features.Memberships.RespondToMembership;

namespace KulturHub.Api.Requests;

public sealed record RespondMembershipRequest(MembershipDecision Decision);
