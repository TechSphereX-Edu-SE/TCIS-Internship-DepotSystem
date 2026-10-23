using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechSpherex.CleanArchitecture.Domain.Entities;

namespace TechSpherex.CleanArchitecture.Infrastructure.Persistence.Configurations;

public sealed class DeliveryOrderLineConfiguration : IEntityTypeConfiguration<DeliveryOrderLine>
{
    public void Configure(EntityTypeBuilder<DeliveryOrderLine> builder)
    {
        builder.ToTable("DeliveryOrderLines", t =>
        {
            // Quantity is positive; released quantity never exceeds it
            t.HasCheckConstraint("CK_DeliveryOrderLines_Quantity",
                "\"Quantity\" > 0 AND \"ReleasedQuantity\" BETWEEN 0 AND \"Quantity\"");
        });

        builder.HasKey(l => l.Id);
        builder.Ignore(l => l.RemainingQuantity);

        builder.Property(l => l.Category).HasConversion<string>().HasMaxLength(20);

        // Q1: lines by type and size; null only on lines from before sizes were recorded (= both sizes)
        builder.Property(l => l.Size).HasConversion<string>().HasMaxLength(10);

        // One line per type and size within an order (DeliveryOrder.AddLine also refuses a no-size line next to a sized one)
        builder.HasIndex(l => new { l.DeliveryOrderId, l.Category, l.Size }).IsUnique();
    }
}
