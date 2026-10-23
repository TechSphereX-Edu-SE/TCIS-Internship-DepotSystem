using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechSpherex.CleanArchitecture.Domain.Entities;

namespace TechSpherex.CleanArchitecture.Infrastructure.Persistence.Configurations;

public sealed class RepairEstimateLineConfiguration : IEntityTypeConfiguration<RepairEstimateLine>
{
    public void Configure(EntityTypeBuilder<RepairEstimateLine> builder)
    {
        builder.ToTable("RepairEstimateLines", t =>
        {
            t.HasCheckConstraint("CK_RepairEstimateLines_Amount", "\"Amount\" >= 0");
            // "Other" names the party; a dispute gives its reason
            t.HasCheckConstraint("CK_RepairEstimateLines_Payer",
                "(\"Payer\" <> 'Other' OR \"PayerName\" IS NOT NULL) AND (\"PayerStatus\" <> 'Disputed' OR \"PayerNote\" IS NOT NULL)");
        });
        builder.HasKey(l => l.Id);

        // The id is set by the domain: an item added to a tracked draft is then inserted, not updated
        builder.Property(l => l.Id).ValueGeneratedNever();

        builder.HasOne<RepairFinding>().WithMany()
            .HasForeignKey(l => l.FindingId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(l => l.DamageDescription).IsRequired().HasMaxLength(RepairEstimateLine.MaxTextLength);
        builder.Property(l => l.RepairMethod).IsRequired().HasMaxLength(RepairEstimateLine.MaxTextLength);
        // Up to 999 999 999 999 999.99 (VND amounts have no decimals, USD has cents)
        builder.Property(l => l.Amount).HasPrecision(18, 2);
        builder.Property(l => l.Payer).HasConversion<string>().HasMaxLength(20);
        builder.Property(l => l.PayerName).HasMaxLength(200);
        builder.Property(l => l.PayerStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(l => l.PayerNote).HasMaxLength(RepairEstimateLine.MaxTextLength);

        builder.HasIndex(l => new { l.EstimateId, l.Position });
    }
}
