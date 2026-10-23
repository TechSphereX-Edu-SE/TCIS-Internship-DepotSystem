using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TechSpherex.CleanArchitecture.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BlockArchiveAndConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ArchivedAt",
                table: "Blocks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PlacementVersion",
                table: "Blocks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Blocks",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "Blocks");

            migrationBuilder.DropColumn(
                name: "PlacementVersion",
                table: "Blocks");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Blocks");
        }
    }
}
