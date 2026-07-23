namespace KulturHub.Domain.Entities;

public class User
{
    public Guid UserId { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public bool IsAdmin { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    private User() { }

    public static User Create(Guid userId, string email, string firstName, string lastName, bool isAdmin = false) => new()
    {
        UserId = userId,
        Email = email,
        FirstName = firstName,
        LastName = lastName,
        IsAdmin = isAdmin,
    };

    public static User Reconstitute(
        Guid userId,
        string email,
        string firstName,
        string lastName,
        bool isAdmin = false,
        bool isDeleted = false,
        DateTime? deletedAt = null) => new()
    {
        UserId = userId,
        Email = email,
        FirstName = firstName,
        LastName = lastName,
        IsAdmin = isAdmin,
        IsDeleted = isDeleted,
        DeletedAt = deletedAt,
    };

    public void MarkAsDeleted()
    {
        if (IsDeleted)
            throw new InvalidOperationException($"User {UserId} is already deleted.");

        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
    }
}
