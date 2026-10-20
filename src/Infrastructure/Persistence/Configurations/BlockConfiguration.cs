using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechSpherex.CleanArchitecture.Domain.Entities;

namespace TechSpherex.CleanArchitecture.Infrastructure.Persistence.Configurations;

public sealed class BlockConfiguration : IEntityTypeConfiguration<Block>
{
    public void Configure(EntityTypeBuilder<Block> builder)
    {
        builder.ToTable("Blocks", t =>
        {
            // Virtual block: no bay/row/tier. Physical block: all dimensions at least 1
            t.HasCheckConstraint("CK_Blocks_Dimensions",
                "(\"IsVirtual\" AND \"MaxBays\" IS NULL AND \"MaxRows\" IS NULL AND \"MaxTiers\" IS NULL) OR " +
                "(NOT \"IsVirtual\" AND \"MaxBays\" >= 1 AND \"MaxRows\" >= 1 AND \"MaxTiers\" >= 1)");
        });

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Code).IsRequired().HasMaxLength(10);
        builder.HasIndex(b => b.Code).IsUnique();

        builder.Property(b => b.Description).HasMaxLength(200);
    }
}
