namespace KulturHub.Domain.Entities;

public class Organisation
{
    private const int MaxNameLength = 200;

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    private Organisation() { }

    public static Organisation Create(string name)
    {
        var trimmed = (name ?? string.Empty).Trim();

        if (trimmed.Length == 0)
            throw new ArgumentException("Organisation name is required.", nameof(name));
        if (trimmed.Length > MaxNameLength)
            throw new ArgumentException($"Organisation name must not exceed {MaxNameLength} characters.", nameof(name));

        return new Organisation
        {
            Id = Guid.NewGuid(),
            Name = trimmed,
            CreatedAt = DateTime.UtcNow,
        };
    }

    public void Rename(string name)
    {
        var trimmed = (name ?? string.Empty).Trim();

        if (trimmed.Length == 0)
            throw new ArgumentException("Organisation name is required.", nameof(name));
        if (trimmed.Length > MaxNameLength)
            throw new ArgumentException($"Organisation name must not exceed {MaxNameLength} characters.", nameof(name));

        Name = trimmed;
    }

    public void MarkAsDeleted()
    {
        if (IsDeleted)
            throw new InvalidOperationException($"Organisation {Id} is already deleted.");

        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
    }

    public static Organisation Reconstitute(
        Guid id,
        string name,
        DateTime createdAt,
        bool isDeleted = false,
        DateTime? deletedAt = null) => new()
    {
        Id = id,
        Name = name,
        CreatedAt = createdAt,
        IsDeleted = isDeleted,
        DeletedAt = deletedAt,
    };
}
