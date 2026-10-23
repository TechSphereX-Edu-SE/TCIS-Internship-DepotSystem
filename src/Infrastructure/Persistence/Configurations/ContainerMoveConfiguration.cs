using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechSpherex.CleanArchitecture.Domain.Entities;

namespace TechSpherex.CleanArchitecture.Infrastructure.Persistence.Configurations;

public sealed class ContainerMoveConfiguration : IEntityTypeConfiguration<ContainerMove>
{
    public void Configure(EntityTypeBuilder<ContainerMove> builder)
    {
        builder.ToTable("ContainerMoves", t =>
        {
            // Same position rule as the visit: all empty (virtual block) or all at least 1
            t.HasCheckConstraint("CK_ContainerMoves_FromPosition",
                "(\"FromBay\" IS NULL AND \"FromRow\" IS NULL AND \"FromTier\" IS NULL) OR " +
                "(\"FromBay\" >= 1 AND \"FromRow\" >= 1 AND \"FromTier\" >= 1)");
            t.HasCheckConstraint("CK_ContainerMoves_ToPosition",
                "(\"ToBay\" IS NULL AND \"ToRow\" IS NULL AND \"ToTier\" IS NULL) OR " +
                "(\"ToBay\" >= 1 AND \"ToRow\" >= 1 AND \"ToTier\" >= 1)");
        });
        builder.HasKey(m => m.Id);

        // A move belongs to one visit; history rows are never deleted with it
        builder.HasOne<ContainerVisit>().WithMany()
            .HasForeignKey(m => m.VisitId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Block>().WithMany()
            .HasForeignKey(m => m.FromBlockId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Block>().WithMany()
            .HasForeignKey(m => m.ToBlockId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(m => m.MovedBy).HasMaxLength(256);

        // The visit detail lists its moves in time order
        builder.HasIndex(m => new { m.VisitId, m.MovedAt });
    }
}
