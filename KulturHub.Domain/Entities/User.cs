namespace KulturHub.Domain.Entities;

public class User
{
    public Guid UserId { get; private set; }
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public bool IsAdmin { get; private set; }

    public static User Create(Guid userId, string firstName, string lastName, bool isAdmin = false) => new()
    {
        UserId = userId,
        FirstName = firstName,
        LastName = lastName,
        IsAdmin = isAdmin,
    };
}
