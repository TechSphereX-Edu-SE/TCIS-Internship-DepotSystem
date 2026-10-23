using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechSpherex.CleanArchitecture.Domain.Entities;

namespace TechSpherex.CleanArchitecture.Infrastructure.Persistence.Configurations;

public sealed class OperatingAssignmentConfiguration : IEntityTypeConfiguration<OperatingAssignment>
{
    public void Configure(EntityTypeBuilder<OperatingAssignment> builder)
    {
        builder.ToTable("OperatingAssignments", t =>
        {
            // Both dates are included; an open end (null) means "until further notice"
            t.HasCheckConstraint("CK_OperatingAssignments_Period",
                "\"ValidTo\" IS NULL OR \"ValidTo\" >= \"ValidFrom\"");

            // A cancelled assignment always says when and why
            t.HasCheckConstraint("CK_OperatingAssignments_Cancel",
                "(\"Status\" = 'Cancelled') = (\"CancelledAt\" IS NOT NULL AND \"CancelReason\" IS NOT NULL)");
        });

        builder.HasKey(a => a.Id);

        builder.HasOne(a => a.Container).WithMany()
            .HasForeignKey(a => a.ContainerId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Operator).WithMany()
            .HasForeignKey(a => a.OperatorId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.Reference).HasMaxLength(50);
        builder.Property(a => a.Note).HasMaxLength(500);
        builder.Property(a => a.CancelReason).HasMaxLength(500);

        // Gate-in looks up the confirmed assignment of one container; the catalog filters by operator
        builder.HasIndex(a => new { a.ContainerId, a.Status });
        builder.HasIndex(a => new { a.OperatorId, a.Status });
    }
}
