using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TechSpherex.CleanArchitecture.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RepairCaseTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RepairCases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContainerId = table.Column<Guid>(type: "uuid", nullable: false),
                    VisitId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    RepairNeeded = table.Column<bool>(type: "boolean", nullable: true),
                    OpenedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepairCases", x => x.Id);
                    table.CheckConstraint("CK_RepairCases_Closed", "(\"Status\" = 'Passed') = (\"ClosedAt\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_RepairCases_ContainerVisits_VisitId",
                        column: x => x.VisitId,
                        principalTable: "ContainerVisits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RepairCases_Containers_ContainerId",
                        column: x => x.ContainerId,
                        principalTable: "Containers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RepairCaseEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RepairCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    FromStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ToStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    By = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepairCaseEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RepairCaseEvents_RepairCases_RepairCaseId",
                        column: x => x.RepairCaseId,
                        principalTable: "RepairCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RepairFindings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RepairCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    VisitId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReportedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepairFindings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RepairFindings_ContainerVisits_VisitId",
                        column: x => x.VisitId,
                        principalTable: "ContainerVisits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RepairFindings_RepairCases_RepairCaseId",
                        column: x => x.RepairCaseId,
                        principalTable: "RepairCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RepairPhotos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RepairCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    FindingId = table.Column<Guid>(type: "uuid", nullable: true),
                    OriginalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    StoredFileName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UploadedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepairPhotos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RepairPhotos_RepairCases_RepairCaseId",
                        column: x => x.RepairCaseId,
                        principalTable: "RepairCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RepairPhotos_RepairFindings_FindingId",
                        column: x => x.FindingId,
                        principalTable: "RepairFindings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RepairCaseEvents_RepairCaseId_At",
                table: "RepairCaseEvents",
                columns: new[] { "RepairCaseId", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_RepairCases_ContainerId_Open",
                table: "RepairCases",
                column: "ContainerId",
                unique: true,
                filter: "\"Status\" <> 'Passed'");

            migrationBuilder.CreateIndex(
                name: "IX_RepairCases_Status_OpenedAt",
                table: "RepairCases",
                columns: new[] { "Status", "OpenedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RepairCases_VisitId",
                table: "RepairCases",
                column: "VisitId");

            migrationBuilder.CreateIndex(
                name: "IX_RepairFindings_RepairCaseId_ReportedAt",
                table: "RepairFindings",
                columns: new[] { "RepairCaseId", "ReportedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RepairFindings_VisitId",
                table: "RepairFindings",
                column: "VisitId");

            migrationBuilder.CreateIndex(
                name: "IX_RepairPhotos_FindingId",
                table: "RepairPhotos",
                column: "FindingId");

            migrationBuilder.CreateIndex(
                name: "IX_RepairPhotos_RepairCaseId",
                table: "RepairPhotos",
                column: "RepairCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_RepairPhotos_StoredFileName",
                table: "RepairPhotos",
                column: "StoredFileName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RepairCaseEvents");

            migrationBuilder.DropTable(
                name: "RepairPhotos");

            migrationBuilder.DropTable(
                name: "RepairFindings");

            migrationBuilder.DropTable(
                name: "RepairCases");
        }
    }
}
