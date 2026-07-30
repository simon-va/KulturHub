using System.Text.RegularExpressions;
using ErrorOr;

namespace KulturHub.Domain.Users;

public sealed class User
{
    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled);

    private User(
        UserId id,
        string email,
        string firstName,
        string lastName,
        bool isAdmin,
        DateTime createdAt,
        bool isDeleted,
        DateTime? deletedAt)
    {
        Id = id;
        Email = email;
        FirstName = firstName;
        LastName = lastName;
        IsAdmin = isAdmin;
        CreatedAt = createdAt;
        IsDeleted = isDeleted;
        DeletedAt = deletedAt;
    }

    public UserId Id { get; }
    public string Email { get; }
    public string FirstName { get; }
    public string LastName { get; }
    public bool IsAdmin { get; }
    public DateTime CreatedAt { get; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    public static ErrorOr<User> Create(
        UserId id,
        string email,
        string firstName,
        string lastName,
        TimeProvider clock,
        bool isAdmin = false)
    {
        if (string.IsNullOrWhiteSpace(email))
            return UserValidationErrors.EmailRequired;

        var trimmedEmail = email.Trim();
        if (trimmedEmail.Length > 255 || !EmailRegex.IsMatch(trimmedEmail))
            return UserValidationErrors.EmailInvalid;

        if (string.IsNullOrWhiteSpace(firstName))
            return UserValidationErrors.FirstNameRequired;

        var trimmedFirstName = firstName.Trim();
        if (trimmedFirstName.Length > 100)
            return UserValidationErrors.FirstNameTooLong;

        if (string.IsNullOrWhiteSpace(lastName))
            return UserValidationErrors.LastNameRequired;

        var trimmedLastName = lastName.Trim();
        if (trimmedLastName.Length > 100)
            return UserValidationErrors.LastNameTooLong;

        var createdAt = clock.GetUtcNow().UtcDateTime;
        if (createdAt.Kind != DateTimeKind.Utc)
            return UserValidationErrors.CreatedAtMustBeUtc;

        return new User(
            id,
            trimmedEmail,
            trimmedFirstName,
            trimmedLastName,
            isAdmin,
            createdAt,
            isDeleted: false,
            deletedAt: null);
    }

    public ErrorOr<Success> Delete(TimeProvider clock)
    {
        if (IsDeleted)
            return Result.Success;

        var now = clock.GetUtcNow().UtcDateTime;
        if (now.Kind != DateTimeKind.Utc)
            return UserValidationErrors.DeletedAtMustBeUtc;

        IsDeleted = true;
        DeletedAt = now;
        return Result.Success;
    }
}
