using ErrorOr;

namespace KulturHub.Domain.Invitations;

public sealed class Invitation
{
    private Invitation(
        InvitationId id,
        string code,
        DateTime createdAt,
        DateTime expiresAt,
        bool isDeleted,
        DateTime? deletedAt,
        Guid? usedBy)
    {
        Id = id;
        Code = code;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
        IsDeleted = isDeleted;
        DeletedAt = deletedAt;
        UsedBy = usedBy;
    }

    public InvitationId Id { get; }
    public string Code { get; }
    public DateTime CreatedAt { get; }
    public DateTime ExpiresAt { get; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public Guid? UsedBy { get; private set; }

    public bool IsUsed => UsedBy.HasValue;

    public static ErrorOr<Invitation> Create(string code, DateTime createdAt, DateTime expiresAt)
    {
        if (string.IsNullOrWhiteSpace(code))
            return InvitationValidationErrors.CodeRequired;

        if (!InvitationCodeGenerator.IsValid(code))
            return InvitationValidationErrors.InvalidFormat;

        if (createdAt.Kind != DateTimeKind.Utc)
            return InvitationValidationErrors.CreatedAtMustBeUtc;

        if (expiresAt.Kind != DateTimeKind.Utc)
            return InvitationValidationErrors.ExpiresAtMustBeUtc;

        if (expiresAt <= createdAt)
            return InvitationValidationErrors.ExpiresAtMustBeAfterCreatedAt;

        return new Invitation(
            InvitationId.New(),
            code,
            createdAt,
            expiresAt,
            isDeleted: false,
            deletedAt: null,
            usedBy: null);
    }

    public void MarkAsUsed(Guid userId) => UsedBy = userId;
}