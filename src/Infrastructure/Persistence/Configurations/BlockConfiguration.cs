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
            // Virtual block: no bay/row/tier. Physical block: bays and rows >= 1, tiers >= 2
            t.HasCheckConstraint("CK_Blocks_Dimensions",
                "(\"IsVirtual\" AND \"MaxBays\" IS NULL AND \"MaxRows\" IS NULL AND \"MaxTiers\" IS NULL) OR " +
                "(NOT \"IsVirtual\" AND \"MaxBays\" >= 1 AND \"MaxRows\" >= 1 AND \"MaxTiers\" >= 2)");
        });

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Code).IsRequired().HasMaxLength(10);
        builder.HasIndex(b => b.Code).IsUnique();

        builder.Property(b => b.Description).HasMaxLength(200);

        // Allowed container types: a PostgreSQL text[] of enum names ('Dry', 'Reefer'...), same as the
        // other enums stored as strings, so the column stays readable in pgAdmin and in SQL reports
        builder.PrimitiveCollection(b => b.AllowedCategories)
            .IsRequired()
            .ElementType()
            .HasConversion<string>();

        builder.Ignore(b => b.IsArchived);

        // PostgreSQL system column xmin as row version: an UPDATE or DELETE of a block that changed
        // since it was read fails with DbUpdateConcurrencyException (mapped to 409 by the API)
        builder.Property(b => b.RowVersion).IsRowVersion();
    }
}
