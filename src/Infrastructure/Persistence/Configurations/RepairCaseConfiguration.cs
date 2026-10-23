using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechSpherex.CleanArchitecture.Domain.Entities;

namespace TechSpherex.CleanArchitecture.Infrastructure.Persistence.Configurations;

public sealed class RepairCaseConfiguration : IEntityTypeConfiguration<RepairCase>
{
    public void Configure(EntityTypeBuilder<RepairCase> builder)
    {
        builder.ToTable("RepairCases", t =>
        {
            // Closed exactly when Passed
            t.HasCheckConstraint("CK_RepairCases_Closed",
                "(\"Status\" = 'Passed') = (\"ClosedAt\" IS NOT NULL)");
        });

        builder.HasKey(c => c.Id);
        builder.Ignore(c => c.IsOpen);

        builder.HasOne(c => c.Container).WithMany()
            .HasForeignKey(c => c.ContainerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.Visit).WithMany()
            .HasForeignKey(c => c.VisitId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(30);

        // xmin: concurrent actions on one case -> 409 Data.ConcurrentUpdate
        builder.Property(c => c.RowVersion).IsRowVersion();

        // One open case per container: a second report while one is open becomes a finding of it,
        // and two reports racing to open a case cannot both succeed
        builder.HasIndex(c => c.ContainerId)
            .IsUnique()
            .HasFilter("\"Status\" <> 'Passed'")
            .HasDatabaseName("IX_RepairCases_ContainerId_Open");

        // Repair page list: by status, newest first
        builder.HasIndex(c => new { c.Status, c.OpenedAt });
    }
}
