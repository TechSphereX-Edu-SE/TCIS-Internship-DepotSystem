using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechSpherex.CleanArchitecture.Domain.Entities;

namespace TechSpherex.CleanArchitecture.Infrastructure.Persistence.Configurations;

public sealed class RepairFindingConfiguration : IEntityTypeConfiguration<RepairFinding>
{
    public void Configure(EntityTypeBuilder<RepairFinding> builder)
    {
        builder.ToTable("RepairFindings");
        builder.HasKey(f => f.Id);

        builder.HasOne<RepairCase>().WithMany()
            .HasForeignKey(f => f.RepairCaseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ContainerVisit>().WithMany()
            .HasForeignKey(f => f.VisitId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(f => f.Description).IsRequired().HasMaxLength(500);
        builder.Property(f => f.Source).HasConversion<string>().HasMaxLength(20);
        builder.Property(f => f.ReportedBy).HasMaxLength(256);
        builder.HasIndex(f => new { f.RepairCaseId, f.ReportedAt });
    }
}
