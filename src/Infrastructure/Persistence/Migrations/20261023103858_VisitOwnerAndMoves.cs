using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TechSpherex.CleanArchitecture.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class VisitOwnerAndMoves : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                table: "ContainerVisits",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ContainerMoves",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VisitId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromBlockId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromBay = table.Column<int>(type: "integer", nullable: true),
                    FromRow = table.Column<int>(type: "integer", nullable: true),
                    FromTier = table.Column<int>(type: "integer", nullable: true),
                    ToBlockId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToBay = table.Column<int>(type: "integer", nullable: true),
                    ToRow = table.Column<int>(type: "integer", nullable: true),
                    ToTier = table.Column<int>(type: "integer", nullable: true),
                    MovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    MovedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContainerMoves", x => x.Id);
                    table.CheckConstraint("CK_ContainerMoves_FromPosition", "(\"FromBay\" IS NULL AND \"FromRow\" IS NULL AND \"FromTier\" IS NULL) OR (\"FromBay\" >= 1 AND \"FromRow\" >= 1 AND \"FromTier\" >= 1)");
                    table.CheckConstraint("CK_ContainerMoves_ToPosition", "(\"ToBay\" IS NULL AND \"ToRow\" IS NULL AND \"ToTier\" IS NULL) OR (\"ToBay\" >= 1 AND \"ToRow\" >= 1 AND \"ToTier\" >= 1)");
                    table.ForeignKey(
                        name: "FK_ContainerMoves_Blocks_FromBlockId",
                        column: x => x.FromBlockId,
                        principalTable: "Blocks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContainerMoves_Blocks_ToBlockId",
                        column: x => x.ToBlockId,
                        principalTable: "Blocks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContainerMoves_ContainerVisits_VisitId",
                        column: x => x.VisitId,
                        principalTable: "ContainerVisits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContainerVisits_GateInAt",
                table: "ContainerVisits",
                column: "GateInAt");

            migrationBuilder.CreateIndex(
                name: "IX_ContainerVisits_GateOutAt",
                table: "ContainerVisits",
                column: "GateOutAt");

            migrationBuilder.CreateIndex(
                name: "IX_ContainerVisits_OwnerId",
                table: "ContainerVisits",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ContainerMoves_FromBlockId",
                table: "ContainerMoves",
                column: "FromBlockId");

            migrationBuilder.CreateIndex(
                name: "IX_ContainerMoves_ToBlockId",
                table: "ContainerMoves",
                column: "ToBlockId");

            migrationBuilder.CreateIndex(
                name: "IX_ContainerMoves_VisitId_MovedAt",
                table: "ContainerMoves",
                columns: new[] { "VisitId", "MovedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_ContainerVisits_LineOperators_OwnerId",
                table: "ContainerVisits",
                column: "OwnerId",
                principalTable: "LineOperators",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContainerVisits_LineOperators_OwnerId",
                table: "ContainerVisits");

            migrationBuilder.DropTable(
                name: "ContainerMoves");

            migrationBuilder.DropIndex(
                name: "IX_ContainerVisits_GateInAt",
                table: "ContainerVisits");

            migrationBuilder.DropIndex(
                name: "IX_ContainerVisits_GateOutAt",
                table: "ContainerVisits");

            migrationBuilder.DropIndex(
                name: "IX_ContainerVisits_OwnerId",
                table: "ContainerVisits");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "ContainerVisits");
        }
    }
}
