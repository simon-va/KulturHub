using KulturHub.Domain.Entities;

namespace KulturHub.Infrastructure.Persistence.Mappings;

internal static class MembershipMapper
{
    internal static Membership ToEntity(MembershipRow row)
    {
        var joinedAt = DateTime.SpecifyKind(row.JoinedAt, DateTimeKind.Utc);
        DateTime? deletedAt = row.DeletedAt is null
            ? null
            : DateTime.SpecifyKind(row.DeletedAt.Value, DateTimeKind.Utc);

        return Membership.Reconstitute(
            row.Id,
            row.UserId,
            row.OrganisationId,
            joinedAt,
            row.IsDeleted,
            deletedAt,
            row.Status,
            row.InvitedBy);
    }

    internal sealed class MembershipRow
    {
        public Guid Id { get; init; }
        public Guid UserId { get; init; }
        public Guid OrganisationId { get; init; }
        public DateTime JoinedAt { get; init; }
        public bool IsDeleted { get; init; }
        public DateTime? DeletedAt { get; init; }
        public MembershipStatus Status { get; init; }
        public Guid? InvitedBy { get; init; }
    }
}
