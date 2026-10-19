using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechSpherex.CleanArchitecture.Domain.Entities;

namespace TechSpherex.CleanArchitecture.Infrastructure.Persistence.Configurations;

public sealed class LineOperatorConfiguration : IEntityTypeConfiguration<LineOperator>
{
    public void Configure(EntityTypeBuilder<LineOperator> builder)
    {
        builder.ToTable("LineOperators");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Code).IsRequired().HasMaxLength(10);
        builder.HasIndex(l => l.Code).IsUnique();

        builder.Property(l => l.Name).IsRequired().HasMaxLength(200);
    }
}
