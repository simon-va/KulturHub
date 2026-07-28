using ErrorOr;

namespace KulturHub.Domain.Organisations;

public sealed class Organisation
{
    public const int MaxNameLength = 200;

    private Organisation(
        OrganisationId id,
        string name,
        DateTime createdAt,
        bool isDeleted,
        DateTime? deletedAt)
    {
        Id = id;
        Name = name;
        CreatedAt = createdAt;
        IsDeleted = isDeleted;
        DeletedAt = deletedAt;
    }

    public OrganisationId Id { get; }
    public string Name { get; private set; }
    public DateTime CreatedAt { get; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    public static ErrorOr<Organisation> Create(string name, TimeProvider clock)
    {
        if (string.IsNullOrWhiteSpace(name))
            return OrganisationValidationErrors.NameRequired;

        var trimmedName = name.Trim();
        if (trimmedName.Length > MaxNameLength)
            return OrganisationValidationErrors.NameTooLong;

        var createdAt = clock.GetUtcNow().UtcDateTime;
        if (createdAt.Kind != DateTimeKind.Utc)
            return OrganisationValidationErrors.CreatedAtMustBeUtc;

        return new Organisation(
            OrganisationId.New(),
            trimmedName,
            createdAt,
            isDeleted: false,
            deletedAt: null);
    }

    public ErrorOr<Success> Update(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return OrganisationValidationErrors.NameRequired;

        if (name.Length > MaxNameLength)
            return OrganisationValidationErrors.NameTooLong;

        Name = name;
        return Result.Success;
    }
}
