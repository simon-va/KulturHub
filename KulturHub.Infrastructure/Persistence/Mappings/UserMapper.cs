using KulturHub.Domain.Entities;

namespace KulturHub.Infrastructure.Persistence.Mappings;

internal static class UserMapper
{
    internal static User ToEntity(UserRow row)
    {
        DateTime? deletedAt = row.DeletedAt is null
            ? null
            : DateTime.SpecifyKind(row.DeletedAt.Value, DateTimeKind.Utc);

        return User.Reconstitute(
            row.UserId,
            row.Email,
            row.FirstName,
            row.LastName,
            row.IsAdmin,
            row.IsDeleted,
            deletedAt);
    }

    internal sealed class UserRow
    {
        public Guid UserId { get; init; }
        public string Email { get; init; } = default!;
        public string FirstName { get; init; } = default!;
        public string LastName { get; init; } = default!;
        public bool IsAdmin { get; init; }
        public bool IsDeleted { get; init; }
        public DateTime? DeletedAt { get; init; }
    }
}
