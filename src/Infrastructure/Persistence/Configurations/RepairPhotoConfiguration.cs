using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechSpherex.CleanArchitecture.Domain.Entities;

namespace TechSpherex.CleanArchitecture.Infrastructure.Persistence.Configurations;

public sealed class RepairPhotoConfiguration : IEntityTypeConfiguration<RepairPhoto>
{
    public void Configure(EntityTypeBuilder<RepairPhoto> builder)
    {
        builder.ToTable("RepairPhotos");
        builder.HasKey(p => p.Id);

        builder.HasOne<RepairCase>().WithMany()
            .HasForeignKey(p => p.RepairCaseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<RepairFinding>().WithMany()
            .HasForeignKey(p => p.FindingId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(p => p.OriginalFileName).IsRequired().HasMaxLength(255);
        builder.Property(p => p.StoredFileName).IsRequired().HasMaxLength(64);
        builder.HasIndex(p => p.StoredFileName).IsUnique();
        builder.Property(p => p.ContentType).IsRequired().HasMaxLength(50);
        builder.Property(p => p.UploadedBy).HasMaxLength(256);
        builder.HasIndex(p => p.RepairCaseId);
    }
}
