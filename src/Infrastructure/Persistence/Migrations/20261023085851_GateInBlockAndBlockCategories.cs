using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TechSpherex.CleanArchitecture.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GateInBlockAndBlockCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Grade",
                table: "ContainerVisits",
                type: "character varying(5)",
                maxLength: 5,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(5)",
                oldMaxLength: 5);

            migrationBuilder.AddColumn<int>(
                name: "GateInBay",
                table: "ContainerVisits",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GateInBlockId",
                table: "ContainerVisits",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GateInRow",
                table: "ContainerVisits",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GateInTier",
                table: "ContainerVisits",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string[]>(
                name: "AllowedCategories",
                table: "Blocks",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            // Existing blocks take every type until someone narrows the list in the Block catalog
            migrationBuilder.Sql("UPDATE \"Blocks\" SET \"AllowedCategories\" = ARRAY['Dry','Reefer','OpenTop','FlatRack','Bunker','Ventilated','Specialized'] WHERE cardinality(\"AllowedCategories\") = 0;");

            migrationBuilder.CreateIndex(
                name: "IX_ContainerVisits_GateInBlockId",
                table: "ContainerVisits",
                column: "GateInBlockId");

            migrationBuilder.AddForeignKey(
                name: "FK_ContainerVisits_Blocks_GateInBlockId",
                table: "ContainerVisits",
                column: "GateInBlockId",
                principalTable: "Blocks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContainerVisits_Blocks_GateInBlockId",
                table: "ContainerVisits");

            migrationBuilder.DropIndex(
                name: "IX_ContainerVisits_GateInBlockId",
                table: "ContainerVisits");

            migrationBuilder.DropColumn(
                name: "GateInBay",
                table: "ContainerVisits");

            migrationBuilder.DropColumn(
                name: "GateInBlockId",
                table: "ContainerVisits");

            migrationBuilder.DropColumn(
                name: "GateInRow",
                table: "ContainerVisits");

            migrationBuilder.DropColumn(
                name: "GateInTier",
                table: "ContainerVisits");

            migrationBuilder.DropColumn(
                name: "AllowedCategories",
                table: "Blocks");

            migrationBuilder.AlterColumn<string>(
                name: "Grade",
                table: "ContainerVisits",
                type: "character varying(5)",
                maxLength: 5,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(5)",
                oldMaxLength: 5,
                oldNullable: true);
        }
    }
}
