using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechSpherex.CleanArchitecture.Domain.Entities;

namespace TechSpherex.CleanArchitecture.Infrastructure.Persistence.Configurations;

public sealed class ContainerConfiguration : IEntityTypeConfiguration<Container>
{
    public void Configure(EntityTypeBuilder<Container> builder)
    {
        builder.ToTable("Containers");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.ContainerNumber).IsRequired().HasMaxLength(11);
        builder.HasIndex(c => c.ContainerNumber).IsUnique();

        builder.Property(c => c.IsoCode).IsRequired().HasMaxLength(4);
        builder.Property(c => c.Size).HasConversion<string>().HasMaxLength(10);
        builder.Property(c => c.Category).HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.OwnerName).IsRequired().HasMaxLength(100);
        builder.Property(c => c.Condition).HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.ConditionNote).HasMaxLength(500);
    }
}
