using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TechSpherex.CleanArchitecture.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PartnersAndOperatingAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsOperator",
                table: "LineOperators",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsOwner",
                table: "LineOperators",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TaxCode",
                table: "LineOperators",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OperatingAssignmentId",
                table: "ContainerVisits",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                table: "Containers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OperatingAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContainerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Reference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ConfirmedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EndedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperatingAssignments", x => x.Id);
                    table.CheckConstraint("CK_OperatingAssignments_Cancel", "(\"Status\" = 'Cancelled') = (\"CancelledAt\" IS NOT NULL AND \"CancelReason\" IS NOT NULL)");
                    table.CheckConstraint("CK_OperatingAssignments_Period", "\"ValidTo\" IS NULL OR \"ValidTo\" >= \"ValidFrom\"");
                    table.ForeignKey(
                        name: "FK_OperatingAssignments_Containers_ContainerId",
                        column: x => x.ContainerId,
                        principalTable: "Containers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperatingAssignments_LineOperators_OperatorId",
                        column: x => x.OperatorId,
                        principalTable: "LineOperators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContainerVisits_OperatingAssignmentId",
                table: "ContainerVisits",
                column: "OperatingAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Containers_OwnerId",
                table: "Containers",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_OperatingAssignments_ContainerId_Status",
                table: "OperatingAssignments",
                columns: new[] { "ContainerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_OperatingAssignments_OperatorId_Status",
                table: "OperatingAssignments",
                columns: new[] { "OperatorId", "Status" });

            migrationBuilder.AddForeignKey(
                name: "FK_Containers_LineOperators_OwnerId",
                table: "Containers",
                column: "OwnerId",
                principalTable: "LineOperators",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ContainerVisits_OperatingAssignments_OperatingAssignmentId",
                table: "ContainerVisits",
                column: "OperatingAssignmentId",
                principalTable: "OperatingAssignments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Every existing partner was a line operator
            migrationBuilder.Sql("UPDATE \"LineOperators\" SET \"IsOperator\" = true;");

            // Reconcile the owner only when the old text is exactly a partner code (no guessing, e.g. "CMA CGM" stays unreconciled)
            migrationBuilder.Sql("UPDATE \"Containers\" c SET \"OwnerId\" = p.\"Id\" FROM \"LineOperators\" p WHERE upper(trim(c.\"OwnerName\")) = p.\"Code\";");
            migrationBuilder.Sql("UPDATE \"LineOperators\" p SET \"IsOwner\" = true WHERE EXISTS (SELECT 1 FROM \"Containers\" c WHERE c.\"OwnerId\" = p.\"Id\");");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Containers_LineOperators_OwnerId",
                table: "Containers");

            migrationBuilder.DropForeignKey(
                name: "FK_ContainerVisits_OperatingAssignments_OperatingAssignmentId",
                table: "ContainerVisits");

            migrationBuilder.DropTable(
                name: "OperatingAssignments");

            migrationBuilder.DropIndex(
                name: "IX_ContainerVisits_OperatingAssignmentId",
                table: "ContainerVisits");

            migrationBuilder.DropIndex(
                name: "IX_Containers_OwnerId",
                table: "Containers");

            migrationBuilder.DropColumn(
                name: "IsOperator",
                table: "LineOperators");

            migrationBuilder.DropColumn(
                name: "IsOwner",
                table: "LineOperators");

            migrationBuilder.DropColumn(
                name: "TaxCode",
                table: "LineOperators");

            migrationBuilder.DropColumn(
                name: "OperatingAssignmentId",
                table: "ContainerVisits");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Containers");
        }
    }
}
