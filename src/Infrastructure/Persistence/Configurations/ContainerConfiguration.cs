using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechSpherex.CleanArchitecture.Domain.Entities;

namespace TechSpherex.CleanArchitecture.Infrastructure.Persistence.Configurations;

public sealed class ContainerConfiguration : IEntityTypeConfiguration<Container>
{
    public void Configure(EntityTypeBuilder<Container> builder)
    {
        builder.ToTable("Containers");

        builder.Property(c => c.ContainerNumber).IsRequired().HasMaxLength(11);
        builder.HasIndex(c => c.ContainerNumber).IsUnique();

        builder.Property(c => c.ContainerType).IsRequired().HasMaxLength(10);

        builder.Property(c => c.Status)
            .HasConversion<string>()
            .HasMaxLength(20);
    }
}
