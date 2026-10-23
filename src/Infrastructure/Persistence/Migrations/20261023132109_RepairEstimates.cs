using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TechSpherex.CleanArchitecture.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RepairEstimates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RepairEstimates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RepairCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Evidence = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SubmittedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DecisionRecordedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ApproverParty = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ApproverName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ApprovalReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DecisionNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepairEstimates", x => x.Id);
                    table.CheckConstraint("CK_RepairEstimates_Approver", "\"Status\" <> 'Approved' OR \"ApproverParty\" IS NOT NULL");
                    table.CheckConstraint("CK_RepairEstimates_Decision", "(\"Status\" IN ('Draft', 'Submitted') AND \"DecidedAt\" IS NULL) OR (\"Status\" IN ('Approved', 'Rejected', 'ChangesRequested') AND \"DecidedAt\" IS NOT NULL AND \"SubmittedAt\" IS NOT NULL)");
                    table.CheckConstraint("CK_RepairEstimates_Version", "\"Version\" >= 1");
                    table.ForeignKey(
                        name: "FK_RepairEstimates_RepairCases_RepairCaseId",
                        column: x => x.RepairCaseId,
                        principalTable: "RepairCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RepairEstimateLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EstimateId = table.Column<Guid>(type: "uuid", nullable: false),
                    FindingId = table.Column<Guid>(type: "uuid", nullable: true),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    DamageDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    RepairMethod = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Payer = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PayerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PayerStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PayerNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepairEstimateLines", x => x.Id);
                    table.CheckConstraint("CK_RepairEstimateLines_Amount", "\"Amount\" >= 0");
                    table.CheckConstraint("CK_RepairEstimateLines_Payer", "(\"Payer\" <> 'Other' OR \"PayerName\" IS NOT NULL) AND (\"PayerStatus\" <> 'Disputed' OR \"PayerNote\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_RepairEstimateLines_RepairEstimates_EstimateId",
                        column: x => x.EstimateId,
                        principalTable: "RepairEstimates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RepairEstimateLines_RepairFindings_FindingId",
                        column: x => x.FindingId,
                        principalTable: "RepairFindings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RepairEstimateLines_EstimateId_Position",
                table: "RepairEstimateLines",
                columns: new[] { "EstimateId", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_RepairEstimateLines_FindingId",
                table: "RepairEstimateLines",
                column: "FindingId");

            migrationBuilder.CreateIndex(
                name: "IX_RepairEstimates_RepairCaseId_Open",
                table: "RepairEstimates",
                column: "RepairCaseId",
                unique: true,
                filter: "\"Status\" IN ('Draft', 'Submitted')");

            migrationBuilder.CreateIndex(
                name: "IX_RepairEstimates_RepairCaseId_Version",
                table: "RepairEstimates",
                columns: new[] { "RepairCaseId", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RepairEstimateLines");

            migrationBuilder.DropTable(
                name: "RepairEstimates");
        }
    }
}
