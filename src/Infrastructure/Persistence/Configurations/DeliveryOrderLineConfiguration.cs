using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechSpherex.CleanArchitecture.Domain.Entities;

namespace TechSpherex.CleanArchitecture.Infrastructure.Persistence.Configurations;

public sealed class DeliveryOrderLineConfiguration : IEntityTypeConfiguration<DeliveryOrderLine>
{
    public void Configure(EntityTypeBuilder<DeliveryOrderLine> builder)
    {
        builder.ToTable("DeliveryOrderLines");
        builder.HasKey(l => l.Id);
        builder.Ignore(l => l.RemainingQuantity);

        builder.Property(l => l.Category).HasConversion<string>().HasMaxLength(20);

        // One line per container category within an order
        builder.HasIndex(l => new { l.DeliveryOrderId, l.Category }).IsUnique();
    }
}
