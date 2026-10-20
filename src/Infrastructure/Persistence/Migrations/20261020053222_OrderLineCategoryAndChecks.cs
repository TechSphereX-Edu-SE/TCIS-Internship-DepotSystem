using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TechSpherex.CleanArchitecture.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OrderLineCategoryAndChecks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DeliveryOrderLines_DeliveryOrderId_Size_Category",
                table: "DeliveryOrderLines");

            migrationBuilder.DropColumn(
                name: "Size",
                table: "DeliveryOrderLines");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryOrderLines_DeliveryOrderId_Category",
                table: "DeliveryOrderLines",
                columns: new[] { "DeliveryOrderId", "Category" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_DeliveryOrderLines_Quantity",
                table: "DeliveryOrderLines",
                sql: "\"Quantity\" > 0 AND \"ReleasedQuantity\" BETWEEN 0 AND \"Quantity\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ContainerVisits_GateOutAfterGateIn",
                table: "ContainerVisits",
                sql: "\"GateOutAt\" IS NULL OR \"GateOutAt\" >= \"GateInAt\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ContainerVisits_GateOutFields",
                table: "ContainerVisits",
                sql: "(\"GateOutAt\" IS NULL AND \"GateOutVehicleNo\" IS NULL AND \"DeliveryOrderId\" IS NULL) OR (\"GateOutAt\" IS NOT NULL AND \"GateOutVehicleNo\" IS NOT NULL AND \"DeliveryOrderId\" IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ContainerVisits_Position",
                table: "ContainerVisits",
                sql: "(\"Bay\" IS NULL AND \"Row\" IS NULL AND \"Tier\" IS NULL) OR (\"Bay\" >= 1 AND \"Row\" >= 1 AND \"Tier\" >= 1)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Containers_Weights",
                table: "Containers",
                sql: "\"TareWeightKg\" > 0 AND \"MaxGrossWeightKg\" > \"TareWeightKg\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Blocks_Dimensions",
                table: "Blocks",
                sql: "(\"IsVirtual\" AND \"MaxBays\" IS NULL AND \"MaxRows\" IS NULL AND \"MaxTiers\" IS NULL) OR (NOT \"IsVirtual\" AND \"MaxBays\" >= 1 AND \"MaxRows\" >= 1 AND \"MaxTiers\" >= 1)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DeliveryOrderLines_DeliveryOrderId_Category",
                table: "DeliveryOrderLines");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DeliveryOrderLines_Quantity",
                table: "DeliveryOrderLines");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ContainerVisits_GateOutAfterGateIn",
                table: "ContainerVisits");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ContainerVisits_GateOutFields",
                table: "ContainerVisits");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ContainerVisits_Position",
                table: "ContainerVisits");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Containers_Weights",
                table: "Containers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Blocks_Dimensions",
                table: "Blocks");

            migrationBuilder.AddColumn<string>(
                name: "Size",
                table: "DeliveryOrderLines",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryOrderLines_DeliveryOrderId_Size_Category",
                table: "DeliveryOrderLines",
                columns: new[] { "DeliveryOrderId", "Size", "Category" },
                unique: true);
        }
    }
}
