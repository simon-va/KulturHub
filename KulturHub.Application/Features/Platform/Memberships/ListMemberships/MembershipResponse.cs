namespace KulturHub.Application.Features.Platform.Memberships.ListMemberships;

public sealed record MembershipResponse(
    Guid Id,
    Guid UserId,
    string FullName,
    string Email,
    DateTime JoinedAt);