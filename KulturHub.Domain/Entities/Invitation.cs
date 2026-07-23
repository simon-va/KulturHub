using System.Security.Cryptography;

namespace KulturHub.Domain.Entities;

public class Invitation
{
    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int SegmentLength = 3;
    private const int ValidityInDays = 7;

    public Guid Id { get; private set; }
    public string Code { get; private set; } = null!;
    public Guid? UsedBy { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    public bool IsExpired => ExpiresAt <= DateTime.UtcNow;
    public bool IsUsed => UsedBy is not null;

    public InvitationValidation EnsureCanBeUsed()
        => IsExpired ? InvitationValidation.Expired
         : IsUsed   ? InvitationValidation.AlreadyUsed
                    : InvitationValidation.Ok;

    private Invitation() { }

    public static Invitation Create() => new()
    {
        Id = Guid.NewGuid(),
        Code = $"{RandomNumberGenerator.GetString(CodeAlphabet, SegmentLength)}-{RandomNumberGenerator.GetString(CodeAlphabet, SegmentLength)}",
        CreatedAt = DateTime.UtcNow,
        ExpiresAt = DateTime.UtcNow.AddDays(ValidityInDays),
    };

    public static Invitation Reconstitute(
        Guid id,
        string code,
        Guid? usedBy,
        DateTime createdAt,
        DateTime expiresAt,
        bool isDeleted = false,
        DateTime? deletedAt = null)
        => new()
        {
            Id = id,
            Code = code,
            UsedBy = usedBy,
            CreatedAt = createdAt,
            ExpiresAt = expiresAt,
            IsDeleted = isDeleted,
            DeletedAt = deletedAt,
        };

    public void MarkAsUsed(Guid userId)
    {
        if (UsedBy is not null)
            throw new InvalidOperationException($"Invitation {Id} has already been used.");

        UsedBy = userId;
    }

    public void MarkAsDeleted()
    {
        if (IsDeleted)
            throw new InvalidOperationException($"Invitation {Id} is already deleted.");

        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
    }
}
