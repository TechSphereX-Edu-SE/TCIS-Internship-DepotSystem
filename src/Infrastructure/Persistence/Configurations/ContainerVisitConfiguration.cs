using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechSpherex.CleanArchitecture.Domain.Entities;

namespace TechSpherex.CleanArchitecture.Infrastructure.Persistence.Configurations;

public sealed class ContainerVisitConfiguration : IEntityTypeConfiguration<ContainerVisit>
{
    public void Configure(EntityTypeBuilder<ContainerVisit> builder)
    {
        builder.ToTable("ContainerVisits");
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

        builder.Property(v => v.Grade).HasConversion<string>().HasMaxLength(5);
        builder.Property(v => v.ConditionIn).HasConversion<string>().HasMaxLength(20);
        builder.Property(v => v.ConditionNoteIn).HasMaxLength(500);
        builder.Property(v => v.GateInVehicleNo).IsRequired().HasMaxLength(20);
        builder.Property(v => v.GateOutVehicleNo).HasMaxLength(20);

        // Mỗi container chỉ có tối đa 1 vòng đời đang mở (chưa gate-out)
        builder.HasIndex(v => v.ContainerId)
            .IsUnique()
            .HasFilter("\"GateOutAt\" IS NULL");

        // Mỗi vị trí Block-Bay-Row-Tier chỉ chứa 1 container đang trong bãi
        builder.HasIndex(v => new { v.BlockId, v.Bay, v.Row, v.Tier })
            .IsUnique()
            .HasFilter("\"GateOutAt\" IS NULL AND \"Bay\" IS NOT NULL");

        // Phục vụ thống kê theo hãng và theo ngày
        builder.HasIndex(v => new { v.LineOperatorId, v.GateInAt });
        builder.HasIndex(v => new { v.LineOperatorId, v.GateOutAt });
    }
}
