namespace KulturHub.Infrastructure.Persistence.Models;

public sealed class InvitationReadRow
{
    public Guid Id { get; init; }
    public string Code { get; init; } = default!;
    public DateTime CreatedAt { get; init; }
    public DateTime ExpiresAt { get; init; }
    public bool IsUsed { get; init; }
    public bool IsExpired { get; init; }
    public Guid? UsedById { get; init; }
    public string? UsedByFirstName { get; init; }
    public string? UsedByLastName { get; init; }
}
