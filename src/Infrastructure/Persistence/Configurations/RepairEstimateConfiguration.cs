using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechSpherex.CleanArchitecture.Domain.Entities;

namespace TechSpherex.CleanArchitecture.Infrastructure.Persistence.Configurations;

public sealed class RepairEstimateConfiguration : IEntityTypeConfiguration<RepairEstimate>
{
    public void Configure(EntityTypeBuilder<RepairEstimate> builder)
    {
        builder.ToTable("RepairEstimates", t =>
        {
            // A decided version always says when; an approved one says by which party
            t.HasCheckConstraint("CK_RepairEstimates_Decision",
                "(\"Status\" IN ('Draft', 'Submitted') AND \"DecidedAt\" IS NULL) OR " +
                "(\"Status\" IN ('Approved', 'Rejected', 'ChangesRequested') AND \"DecidedAt\" IS NOT NULL AND \"SubmittedAt\" IS NOT NULL)");
            t.HasCheckConstraint("CK_RepairEstimates_Approver",
                "\"Status\" <> 'Approved' OR \"ApproverParty\" IS NOT NULL");
            t.HasCheckConstraint("CK_RepairEstimates_Version", "\"Version\" >= 1");
        });
        builder.HasKey(e => e.Id);
        builder.Ignore(e => e.Total);
        builder.Ignore(e => e.IsOpen);

        builder.HasOne<RepairCase>().WithMany()
            .HasForeignKey(e => e.RepairCaseId).OnDelete(DeleteBehavior.Restrict);

        // Items belong to their version: removed from a draft = deleted
        builder.HasMany(e => e.Lines).WithOne()
            .HasForeignKey(l => l.EstimateId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(e => e.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(e => e.Currency).IsRequired().HasMaxLength(3);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Note).HasMaxLength(1000);
        builder.Property(e => e.Evidence).HasMaxLength(1000);
        builder.Property(e => e.SubmittedBy).HasMaxLength(256);
        builder.Property(e => e.DecisionRecordedBy).HasMaxLength(256);
        builder.Property(e => e.ApproverParty).HasMaxLength(200);
        builder.Property(e => e.ApproverName).HasMaxLength(200);
        builder.Property(e => e.ApprovalReference).HasMaxLength(200);
        builder.Property(e => e.DecisionNote).HasMaxLength(1000);

        // xmin: two decisions on one version -> 409 Data.ConcurrentUpdate
        builder.Property(e => e.RowVersion).IsRowVersion();

        // Version numbers are unique per case; at most one open (Draft or Submitted) version per case
        builder.HasIndex(e => new { e.RepairCaseId, e.Version }).IsUnique();
        builder.HasIndex(e => e.RepairCaseId)
            .IsUnique()
            .HasFilter("\"Status\" IN ('Draft', 'Submitted')")
            .HasDatabaseName("IX_RepairEstimates_RepairCaseId_Open");
    }
}
