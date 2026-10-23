using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechSpherex.CleanArchitecture.Domain.Entities;

namespace TechSpherex.CleanArchitecture.Infrastructure.Persistence.Configurations;

public sealed class DemoDatasetConfiguration : IEntityTypeConfiguration<DemoDataset>
{
    public void Configure(EntityTypeBuilder<DemoDataset> builder)
    {
        builder.ToTable("DemoDatasets");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Key).IsRequired().HasMaxLength(50);
        builder.HasIndex(d => d.Key).IsUnique();
    }
}
