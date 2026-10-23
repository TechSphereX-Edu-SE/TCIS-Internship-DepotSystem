using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechSpherex.CleanArchitecture.Domain.Entities;

namespace TechSpherex.CleanArchitecture.Infrastructure.Persistence.Configurations;

public sealed class DeliveryOrderConfiguration : IEntityTypeConfiguration<DeliveryOrder>
{
    public void Configure(EntityTypeBuilder<DeliveryOrder> builder)
    {
        builder.ToTable("DeliveryOrders", t =>
        {
            // Q7: a cancelled order always has its reason
            t.HasCheckConstraint("CK_DeliveryOrders_Cancel",
                "(\"CancelledAt\" IS NULL AND \"CancelReason\" IS NULL) OR " +
                "(\"CancelledAt\" IS NOT NULL AND \"CancelReason\" IS NOT NULL)");
        });
        builder.HasKey(o => o.Id);
        builder.Ignore(o => o.IsCancelled);

        builder.Property(o => o.OrderNumber).IsRequired().HasMaxLength(30);
        builder.HasIndex(o => o.OrderNumber).IsUnique();

        builder.Property(o => o.VesselVoyage).IsRequired().HasMaxLength(50);
        builder.Property(o => o.CancelReason).HasMaxLength(500);

        // xmin: concurrent releases / cancel on one order -> 409 Data.ConcurrentUpdate
        builder.Property(o => o.RowVersion).IsRowVersion();

        builder.HasOne(o => o.LineOperator).WithMany()
            .HasForeignKey(o => o.LineOperatorId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.Customer).WithMany()
            .HasForeignKey(o => o.CustomerId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(o => o.Lines).WithOne()
            .HasForeignKey(l => l.DeliveryOrderId).OnDelete(DeleteBehavior.Cascade);

        // EF reads/writes order lines through the _lines field (Lines is read-only)
        builder.Navigation(o => o.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
