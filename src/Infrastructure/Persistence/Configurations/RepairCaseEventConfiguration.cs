using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechSpherex.CleanArchitecture.Domain.Entities;

namespace TechSpherex.CleanArchitecture.Infrastructure.Persistence.Configurations;

public sealed class RepairCaseEventConfiguration : IEntityTypeConfiguration<RepairCaseEvent>
{
    public void Configure(EntityTypeBuilder<RepairCaseEvent> builder)
    {
        builder.ToTable("RepairCaseEvents");
        builder.HasKey(e => e.Id);

        builder.HasOne<RepairCase>().WithMany()
            .HasForeignKey(e => e.RepairCaseId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(e => e.Kind).HasConversion<string>().HasMaxLength(30);
        builder.Property(e => e.FromStatus).HasConversion<string>().HasMaxLength(30);
        builder.Property(e => e.ToStatus).HasConversion<string>().HasMaxLength(30);
        builder.Property(e => e.Note).HasMaxLength(500);
        builder.Property(e => e.By).HasMaxLength(256);
        builder.HasIndex(e => new { e.RepairCaseId, e.At });
    }
}
