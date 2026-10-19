using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TechSpherex.CleanArchitecture.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DepotDataModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM \"Containers\";");

            migrationBuilder.DropColumn(
                name: "DepartedAt",
                table: "Containers");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "Containers",
                newName: "Condition");

            migrationBuilder.RenameColumn(
                name: "ContainerType",
                table: "Containers",
                newName: "Size");

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Containers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ConditionNote",
                table: "Containers",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IsoCode",
                table: "Containers",
                type: "character varying(4)",
                maxLength: 4,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateOnly>(
                name: "ManufactureDate",
                table: "Containers",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxGrossWeightKg",
                table: "Containers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "OwnerName",
                table: "Containers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "TareWeightKg",
                table: "Containers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Blocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsVirtual = table.Column<bool>(type: "boolean", nullable: false),
                    MaxBays = table.Column<int>(type: "integer", nullable: true),
                    MaxRows = table.Column<int>(type: "integer", nullable: true),
                    MaxTiers = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Blocks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LineOperators",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LineOperators", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeliveryOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    LineOperatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    VesselVoyage = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeliveryOrders_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DeliveryOrders_LineOperators_LineOperatorId",
                        column: x => x.LineOperatorId,
                        principalTable: "LineOperators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContainerVisits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContainerId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineOperatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    BlockId = table.Column<Guid>(type: "uuid", nullable: false),
                    Bay = table.Column<int>(type: "integer", nullable: true),
                    Row = table.Column<int>(type: "integer", nullable: true),
                    Tier = table.Column<int>(type: "integer", nullable: true),
                    Grade = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    ConditionIn = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ConditionNoteIn = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    GateInAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    GateInVehicleNo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    GateOutAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    GateOutVehicleNo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    DeliveryOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContainerVisits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContainerVisits_Blocks_BlockId",
                        column: x => x.BlockId,
                        principalTable: "Blocks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContainerVisits_Containers_ContainerId",
                        column: x => x.ContainerId,
                        principalTable: "Containers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContainerVisits_DeliveryOrders_DeliveryOrderId",
                        column: x => x.DeliveryOrderId,
                        principalTable: "DeliveryOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContainerVisits_LineOperators_LineOperatorId",
                        column: x => x.LineOperatorId,
                        principalTable: "LineOperators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DeliveryOrderLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeliveryOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Size = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    ReleasedQuantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryOrderLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeliveryOrderLines_DeliveryOrders_DeliveryOrderId",
                        column: x => x.DeliveryOrderId,
                        principalTable: "DeliveryOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Blocks_Code",
                table: "Blocks",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContainerVisits_BlockId_Bay_Row_Tier",
                table: "ContainerVisits",
                columns: new[] { "BlockId", "Bay", "Row", "Tier" },
                unique: true,
                filter: "\"GateOutAt\" IS NULL AND \"Bay\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ContainerVisits_ContainerId",
                table: "ContainerVisits",
                column: "ContainerId",
                unique: true,
                filter: "\"GateOutAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ContainerVisits_DeliveryOrderId",
                table: "ContainerVisits",
                column: "DeliveryOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ContainerVisits_LineOperatorId_GateInAt",
                table: "ContainerVisits",
                columns: new[] { "LineOperatorId", "GateInAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ContainerVisits_LineOperatorId_GateOutAt",
                table: "ContainerVisits",
                columns: new[] { "LineOperatorId", "GateOutAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_TaxCode",
                table: "Customers",
                column: "TaxCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryOrderLines_DeliveryOrderId_Size_Category",
                table: "DeliveryOrderLines",
                columns: new[] { "DeliveryOrderId", "Size", "Category" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryOrders_CustomerId",
                table: "DeliveryOrders",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryOrders_LineOperatorId",
                table: "DeliveryOrders",
                column: "LineOperatorId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryOrders_OrderNumber",
                table: "DeliveryOrders",
                column: "OrderNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LineOperators_Code",
                table: "LineOperators",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContainerVisits");

            migrationBuilder.DropTable(
                name: "DeliveryOrderLines");

            migrationBuilder.DropTable(
                name: "Blocks");

            migrationBuilder.DropTable(
                name: "DeliveryOrders");

            migrationBuilder.DropTable(
                name: "Customers");

            migrationBuilder.DropTable(
                name: "LineOperators");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Containers");

            migrationBuilder.DropColumn(
                name: "ConditionNote",
                table: "Containers");

            migrationBuilder.DropColumn(
                name: "IsoCode",
                table: "Containers");

            migrationBuilder.DropColumn(
                name: "ManufactureDate",
                table: "Containers");

            migrationBuilder.DropColumn(
                name: "MaxGrossWeightKg",
                table: "Containers");

            migrationBuilder.DropColumn(
                name: "OwnerName",
                table: "Containers");

            migrationBuilder.DropColumn(
                name: "TareWeightKg",
                table: "Containers");

            migrationBuilder.RenameColumn(
                name: "Size",
                table: "Containers",
                newName: "ContainerType");

            migrationBuilder.RenameColumn(
                name: "Condition",
                table: "Containers",
                newName: "Status");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DepartedAt",
                table: "Containers",
                type: "timestamp with time zone",
                nullable: true);
        }
    }
}
