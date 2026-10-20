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

        // One line per container category within an order
        builder.HasIndex(l => new { l.DeliveryOrderId, l.Category }).IsUnique();
    }
}
