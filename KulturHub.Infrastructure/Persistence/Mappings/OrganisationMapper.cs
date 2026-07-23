using KulturHub.Domain.Entities;

namespace KulturHub.Infrastructure.Persistence.Mappings;

internal static class OrganisationMapper
{
    internal static Organisation ToEntity(OrganisationRow row)
    {
        var createdAt = DateTime.SpecifyKind(row.CreatedAt, DateTimeKind.Utc);
        DateTime? deletedAt = row.DeletedAt is null
            ? null
            : DateTime.SpecifyKind(row.DeletedAt.Value, DateTimeKind.Utc);

        return Organisation.Reconstitute(
            row.Id,
            row.Name,
            createdAt,
            row.IsDeleted,
            deletedAt);
    }

    internal sealed class OrganisationRow
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = default!;
        public DateTime CreatedAt { get; init; }
        public bool IsDeleted { get; init; }
        public DateTime? DeletedAt { get; init; }
    }
}
