using ErrorOr;
using KulturHub.Domain.Organisations;
using KulturHub.Domain.Users;

namespace KulturHub.Domain.ChangeLogs;

public sealed class ChangeLog
{
    public const int MaxMessageLength = 500;

    private ChangeLog(
        ChangeLogId id,
        OrganisationId organisationId,
        UserId createdBy,
        string message,
        ChangeLogCategory category,
        IReadOnlyDictionary<string, string?> data,
        DateTime createdAt,
        bool isDeleted,
        DateTime? deletedAt)
    {
        Id = id;
        OrganisationId = organisationId;
        CreatedBy = createdBy;
        Message = message;
        Category = category;
        Data = data;
        CreatedAt = createdAt;
        IsDeleted = isDeleted;
        DeletedAt = deletedAt;
    }

    public ChangeLogId Id { get; }
    public OrganisationId OrganisationId { get; }
    public UserId CreatedBy { get; }
    public string Message { get; }
    public ChangeLogCategory Category { get; }
    public IReadOnlyDictionary<string, string?> Data { get; }
    public DateTime CreatedAt { get; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    public static ErrorOr<ChangeLog> Create(
        OrganisationId organisationId,
        UserId createdBy,
        string message,
        ChangeLogCategory category,
        IReadOnlyDictionary<string, string?> data,
        TimeProvider clock)
    {
        if (organisationId.Value == Guid.Empty)
            return ChangeLogValidationErrors.OrganisationIdRequired;

        if (createdBy.Value == Guid.Empty)
            return ChangeLogValidationErrors.CreatedByRequired;

        if (string.IsNullOrWhiteSpace(message))
            return ChangeLogValidationErrors.MessageRequired;

        var trimmedMessage = message.Trim();
        if (trimmedMessage.Length > MaxMessageLength)
            return ChangeLogValidationErrors.MessageTooLong;

        if (!Enum.IsDefined(typeof(ChangeLogCategory), category))
            return ChangeLogValidationErrors.CategoryRequired;

        if (data is null)
            return ChangeLogValidationErrors.DataRequired;

        var createdAt = clock.GetUtcNow().UtcDateTime;
        if (createdAt.Kind != DateTimeKind.Utc)
            return ChangeLogValidationErrors.CreatedAtMustBeUtc;

        return new ChangeLog(
            ChangeLogId.New(),
            organisationId,
            createdBy,
            trimmedMessage,
            category,
            data,
            createdAt,
            isDeleted: false,
            deletedAt: null);
    }
}