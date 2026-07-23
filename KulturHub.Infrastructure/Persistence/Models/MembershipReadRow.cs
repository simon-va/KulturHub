using KulturHub.Domain.Entities;

namespace KulturHub.Infrastructure.Persistence.Models;

public sealed class MembershipReadRow
{
    public Guid MembershipId { get; init; }
    public Guid UserId { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? Email { get; init; }
    public MembershipStatus Status { get; init; }
    public DateTime JoinedAt { get; init; }
}
