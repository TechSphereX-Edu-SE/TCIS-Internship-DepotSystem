using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechSpherex.CleanArchitecture.Domain.Entities;

namespace TechSpherex.CleanArchitecture.Infrastructure.Persistence.Configurations;

public sealed class ExternalRepairTransferConfiguration : IEntityTypeConfiguration<ExternalRepairTransfer>
{
    public void Configure(EntityTypeBuilder<ExternalRepairTransfer> builder)
    {
        builder.ToTable("ExternalRepairTransfers", t =>
        {
            // Each status has its own dates: a gated-out transfer has its gate-out visit, a returned one also its return
            t.HasCheckConstraint("CK_ExternalRepairTransfers_Status",
                "(\"Status\" = 'Requested' AND \"GateOutVisitId\" IS NULL AND \"CancelledAt\" IS NULL) OR " +
                "(\"Status\" = 'Cancelled' AND \"GateOutVisitId\" IS NULL AND \"CancelledAt\" IS NOT NULL AND \"CancelReason\" IS NOT NULL) OR " +
                "(\"Status\" = 'GatedOut' AND \"GateOutVisitId\" IS NOT NULL AND \"ReturnVisitId\" IS NULL) OR " +
                "(\"Status\" = 'Returned' AND \"GateOutVisitId\" IS NOT NULL AND \"ReturnVisitId\" IS NOT NULL)");
        });

        builder.HasKey(t => t.Id);
        builder.Ignore(t => t.IsActive);

        builder.HasOne(t => t.RepairCase).WithMany()
            .HasForeignKey(t => t.RepairCaseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(t => t.Container).WithMany()
            .HasForeignKey(t => t.ContainerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ContainerVisit>().WithMany()
            .HasForeignKey(t => t.GateOutVisitId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ContainerVisit>().WithMany()
            .HasForeignKey(t => t.ReturnVisitId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(t => t.Facility).IsRequired().HasMaxLength(200);
        builder.Property(t => t.Reason).IsRequired().HasMaxLength(500);
        builder.Property(t => t.Reference).IsRequired().HasMaxLength(100);
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.RequestedBy).HasMaxLength(256);
        builder.Property(t => t.CancelReason).HasMaxLength(500);
        builder.Property(t => t.RowVersion).IsRowVersion();

        // One active transfer (waiting to leave or at the facility) per case
        builder.HasIndex(t => t.RepairCaseId)
            .IsUnique()
            .HasFilter("\"Status\" IN ('Requested', 'GatedOut')")
            .HasDatabaseName("IX_ExternalRepairTransfers_RepairCaseId_Active");

        // The gate-out page lists the transfers waiting to leave; gate-in looks up a container at a facility
        builder.HasIndex(t => new { t.Status, t.RequestedAt });
        builder.HasIndex(t => new { t.ContainerId, t.Status });
    }
}
