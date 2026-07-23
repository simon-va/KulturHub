namespace KulturHub.Infrastructure.Persistence.Models;

public sealed class PendingMembershipReadRow
{
    public Guid MembershipId { get; init; }
    public string? InviterFirstName { get; init; }
    public string? InviterLastName { get; init; }
    public string OrganisationName { get; init; } = string.Empty;
    public DateTime JoinedAt { get; init; }
}
