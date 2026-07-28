using KulturHub.Domain.ChangeLogs;
using KulturHub.Domain.Organisations;
using KulturHub.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KulturHub.Infrastructure.Persistence.Configurations;

public sealed class ChangeLogConfiguration : IEntityTypeConfiguration<ChangeLog>
{
    private static readonly ValueComparer<IReadOnlyDictionary<string, string?>> DataComparer = new(
        (a, b) => (a == null && b == null) || (a != null && b != null && a.SequenceEqual(b)),
        v => v == null
            ? 0
            : v.Aggregate(0, (acc, kv) => HashCode.Combine(acc, kv.Key.GetHashCode(), kv.Value == null ? 0 : kv.Value.GetHashCode())),
        v => (IReadOnlyDictionary<string, string?>)v.ToDictionary(kv => kv.Key, kv => kv.Value));

    public void Configure(EntityTypeBuilder<ChangeLog> builder)
    {
        builder.ToTable("change_logs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, v => ChangeLogId.From(v));

        builder.Property(x => x.OrganisationId)
            .HasColumnName("organisation_id")
            .HasConversion(id => id.Value, v => OrganisationId.From(v))
            .IsRequired();

        builder.Property(x => x.CreatedBy)
            .HasColumnName("created_by")
            .HasConversion(id => id.Value, v => UserId.From(v))
            .IsRequired();

        builder.Property(x => x.Message)
            .HasColumnName("message")
            .HasMaxLength(ChangeLog.MaxMessageLength)
            .IsRequired();

        builder.Property(x => x.Data)
            .HasColumnName("data")
            .HasColumnType("jsonb")
            .HasConversion(
                v => ChangeLogDataJson.Serialize(v),
                v => ChangeLogDataJson.Deserialize(v))
            .Metadata.SetValueComparer(DataComparer);

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(x => x.DeletedAt)
            .HasColumnName("deleted_at");

        builder.HasIndex(x => x.OrganisationId);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
