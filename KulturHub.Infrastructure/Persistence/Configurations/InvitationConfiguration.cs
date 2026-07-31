using KulturHub.Domain.Invitations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KulturHub.Infrastructure.Persistence.Configurations;

public sealed class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> builder)
    {
        builder.ToTable("invitation_codes");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, v => new InvitationId(v));

        builder.Property(x => x.Code)
            .HasColumnName("code")
            .HasMaxLength(4)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        builder.Property(x => x.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(x => x.DeletedAt)
            .HasColumnName("deleted_at");

        builder.Property(x => x.UsedBy)
            .HasColumnName("used_by");

        builder.HasIndex(x => x.Code).IsUnique();

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
