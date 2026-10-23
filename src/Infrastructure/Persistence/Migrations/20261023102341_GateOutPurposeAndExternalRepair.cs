using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TechSpherex.CleanArchitecture.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GateOutPurposeAndExternalRepair : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DeliveryOrderLines_DeliveryOrderId_Category",
                table: "DeliveryOrderLines");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ContainerVisits_GateOutFields",
                table: "ContainerVisits");

            migrationBuilder.AddColumn<string>(
                name: "CancelReason",
                table: "DeliveryOrders",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CancelledAt",
                table: "DeliveryOrders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastReleasedAt",
                table: "DeliveryOrders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "DeliveryOrders",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<string>(
                name: "Size",
                table: "DeliveryOrderLines",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExitPurpose",
                table: "ContainerVisits",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            // Visits that left before this change were all empty releases (with a delivery order)
            migrationBuilder.Sql("UPDATE \"ContainerVisits\" SET \"ExitPurpose\" = 'EmptyRelease' WHERE \"GateOutAt\" IS NOT NULL;");

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "ContainerVisits",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateTable(
                name: "ExternalRepairTransfers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RepairCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContainerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Facility = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RequestedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    GateOutVisitId = table.Column<Guid>(type: "uuid", nullable: true),
                    GatedOutAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReturnVisitId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReturnedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalRepairTransfers", x => x.Id);
                    table.CheckConstraint("CK_ExternalRepairTransfers_Status", "(\"Status\" = 'Requested' AND \"GateOutVisitId\" IS NULL AND \"CancelledAt\" IS NULL) OR (\"Status\" = 'Cancelled' AND \"GateOutVisitId\" IS NULL AND \"CancelledAt\" IS NOT NULL AND \"CancelReason\" IS NOT NULL) OR (\"Status\" = 'GatedOut' AND \"GateOutVisitId\" IS NOT NULL AND \"ReturnVisitId\" IS NULL) OR (\"Status\" = 'Returned' AND \"GateOutVisitId\" IS NOT NULL AND \"ReturnVisitId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_ExternalRepairTransfers_ContainerVisits_GateOutVisitId",
                        column: x => x.GateOutVisitId,
                        principalTable: "ContainerVisits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExternalRepairTransfers_ContainerVisits_ReturnVisitId",
                        column: x => x.ReturnVisitId,
                        principalTable: "ContainerVisits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExternalRepairTransfers_Containers_ContainerId",
                        column: x => x.ContainerId,
                        principalTable: "Containers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExternalRepairTransfers_RepairCases_RepairCaseId",
                        column: x => x.RepairCaseId,
                        principalTable: "RepairCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_DeliveryOrders_Cancel",
                table: "DeliveryOrders",
                sql: "(\"CancelledAt\" IS NULL AND \"CancelReason\" IS NULL) OR (\"CancelledAt\" IS NOT NULL AND \"CancelReason\" IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryOrderLines_DeliveryOrderId_Category_Size",
                table: "DeliveryOrderLines",
                columns: new[] { "DeliveryOrderId", "Category", "Size" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ContainerVisits_GateOutFields",
                table: "ContainerVisits",
                sql: "(\"GateOutAt\" IS NULL AND \"GateOutVehicleNo\" IS NULL AND \"DeliveryOrderId\" IS NULL AND \"ExitPurpose\" IS NULL) OR (\"GateOutAt\" IS NOT NULL AND \"GateOutVehicleNo\" IS NOT NULL AND ((\"ExitPurpose\" = 'EmptyRelease' AND \"DeliveryOrderId\" IS NOT NULL) OR (\"ExitPurpose\" = 'ExternalRepair' AND \"DeliveryOrderId\" IS NULL)))");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalRepairTransfers_ContainerId_Status",
                table: "ExternalRepairTransfers",
                columns: new[] { "ContainerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalRepairTransfers_GateOutVisitId",
                table: "ExternalRepairTransfers",
                column: "GateOutVisitId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalRepairTransfers_RepairCaseId_Active",
                table: "ExternalRepairTransfers",
                column: "RepairCaseId",
                unique: true,
                filter: "\"Status\" IN ('Requested', 'GatedOut')");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalRepairTransfers_ReturnVisitId",
                table: "ExternalRepairTransfers",
                column: "ReturnVisitId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalRepairTransfers_Status_RequestedAt",
                table: "ExternalRepairTransfers",
                columns: new[] { "Status", "RequestedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExternalRepairTransfers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DeliveryOrders_Cancel",
                table: "DeliveryOrders");

            migrationBuilder.DropIndex(
                name: "IX_DeliveryOrderLines_DeliveryOrderId_Category_Size",
                table: "DeliveryOrderLines");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ContainerVisits_GateOutFields",
                table: "ContainerVisits");

            migrationBuilder.DropColumn(
                name: "CancelReason",
                table: "DeliveryOrders");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "DeliveryOrders");

            migrationBuilder.DropColumn(
                name: "LastReleasedAt",
                table: "DeliveryOrders");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "DeliveryOrders");

            migrationBuilder.DropColumn(
                name: "Size",
                table: "DeliveryOrderLines");

            migrationBuilder.DropColumn(
                name: "ExitPurpose",
                table: "ContainerVisits");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "ContainerVisits");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryOrderLines_DeliveryOrderId_Category",
                table: "DeliveryOrderLines",
                columns: new[] { "DeliveryOrderId", "Category" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ContainerVisits_GateOutFields",
                table: "ContainerVisits",
                sql: "(\"GateOutAt\" IS NULL AND \"GateOutVehicleNo\" IS NULL AND \"DeliveryOrderId\" IS NULL) OR (\"GateOutAt\" IS NOT NULL AND \"GateOutVehicleNo\" IS NOT NULL AND \"DeliveryOrderId\" IS NOT NULL)");
        }
    }
}
