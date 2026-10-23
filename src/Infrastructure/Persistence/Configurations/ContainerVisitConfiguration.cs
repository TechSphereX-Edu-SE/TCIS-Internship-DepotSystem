using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechSpherex.CleanArchitecture.Domain.Entities;

namespace TechSpherex.CleanArchitecture.Infrastructure.Persistence.Configurations;

public sealed class ContainerVisitConfiguration : IEntityTypeConfiguration<ContainerVisit>
{
    public void Configure(EntityTypeBuilder<ContainerVisit> builder)
    {
        builder.ToTable("ContainerVisits", t =>
        {
            // Bay/Row/Tier are either all empty (virtual block) or all at least 1
            t.HasCheckConstraint("CK_ContainerVisits_Position",
                "(\"Bay\" IS NULL AND \"Row\" IS NULL AND \"Tier\" IS NULL) OR " +
                "(\"Bay\" >= 1 AND \"Row\" >= 1 AND \"Tier\" >= 1)");

            // Gate-out cannot happen before gate-in
            t.HasCheckConstraint("CK_ContainerVisits_GateOutAfterGateIn",
                "\"GateOutAt\" IS NULL OR \"GateOutAt\" >= \"GateInAt\"");

            // Gate-out time, vehicle and delivery order are set together
            t.HasCheckConstraint("CK_ContainerVisits_GateOutFields",
                "(\"GateOutAt\" IS NULL AND \"GateOutVehicleNo\" IS NULL AND \"DeliveryOrderId\" IS NULL) OR " +
                "(\"GateOutAt\" IS NOT NULL AND \"GateOutVehicleNo\" IS NOT NULL AND \"DeliveryOrderId\" IS NOT NULL)");
        });

        builder.HasKey(v => v.Id);
        builder.Ignore(v => v.IsInYard);

        builder.HasOne(v => v.Container).WithMany()
            .HasForeignKey(v => v.ContainerId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(v => v.LineOperator).WithMany()
            .HasForeignKey(v => v.LineOperatorId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(v => v.Block).WithMany()
            .HasForeignKey(v => v.BlockId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(v => v.DeliveryOrder).WithMany()
            .HasForeignKey(v => v.DeliveryOrderId).OnDelete(DeleteBehavior.Restrict);

        // Block at gate-in (history); null on visits recorded before it was stored
        builder.HasOne(v => v.GateInBlock).WithMany()
            .HasForeignKey(v => v.GateInBlockId).OnDelete(DeleteBehavior.Restrict);

        // Legacy A/B/C grade: nullable, only older visits have one
        builder.Property(v => v.Grade).HasConversion<string>().HasMaxLength(5);
        builder.Property(v => v.ConditionIn).HasConversion<string>().HasMaxLength(20);
        builder.Property(v => v.ConditionNoteIn).HasMaxLength(500);
        builder.Property(v => v.GateInVehicleNo).IsRequired().HasMaxLength(20);
        builder.Property(v => v.GateOutVehicleNo).HasMaxLength(20);

        // At most one open visit (not gated out) per container
        builder.HasIndex(v => v.ContainerId)
            .IsUnique()
            .HasFilter("\"GateOutAt\" IS NULL");

        // Each Block-Bay-Row-Tier slot holds at most one container in the yard
        builder.HasIndex(v => new { v.BlockId, v.Bay, v.Row, v.Tier })
            .IsUnique()
            .HasFilter("\"GateOutAt\" IS NULL AND \"Bay\" IS NOT NULL");

        // Support statistics by line operator and by day
        builder.HasIndex(v => new { v.LineOperatorId, v.GateInAt });
        builder.HasIndex(v => new { v.LineOperatorId, v.GateOutAt });
    }
}
