namespace KulturHub.Domain.Entities;

public class Invitation
{
    public Guid Id { get; private set; }
    public string Code { get; private set; } = null!;
    public Guid? UsedBy { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }

    public bool IsExpired => ExpiresAt <= DateTime.UtcNow;
    public bool IsUsed => UsedBy is not null;

    private Invitation() { }

    public static Invitation Create() => new()
    {
        Id = Guid.NewGuid(),
        Code = Guid.NewGuid().ToString("N").ToUpper()[..8],
        CreatedAt = DateTime.UtcNow,
        ExpiresAt = DateTime.UtcNow.AddDays(7),
    };

    public static Invitation Reconstitute(Guid id, string code, Guid? usedBy, DateTime createdAt, DateTime expiresAt)
        => new() { Id = id, Code = code, UsedBy = usedBy, CreatedAt = createdAt, ExpiresAt = expiresAt };
}
