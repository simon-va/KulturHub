using KulturHub.Domain.Entities;

namespace KulturHub.Infrastructure.Persistence.Mappings;

internal static class InvitationMapper
{
    internal static Invitation ToEntity(InvitationRow row)
    {
        var createdAt = DateTime.SpecifyKind(row.CreatedAt, DateTimeKind.Utc);
        var expiresAt = DateTime.SpecifyKind(row.ExpiresAt, DateTimeKind.Utc);
        DateTime? deletedAt = row.DeletedAt is null
            ? null
            : DateTime.SpecifyKind(row.DeletedAt.Value, DateTimeKind.Utc);

        return Invitation.Reconstitute(
            row.Id,
            row.Code,
            row.UsedBy,
            createdAt,
            expiresAt,
            row.IsDeleted,
            deletedAt);
    }

    internal sealed class InvitationRow
    {
        public Guid Id { get; init; }
        public string Code { get; init; } = default!;
        public Guid? UsedBy { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime ExpiresAt { get; init; }
        public bool IsDeleted { get; init; }
        public DateTime? DeletedAt { get; init; }
    }
}
