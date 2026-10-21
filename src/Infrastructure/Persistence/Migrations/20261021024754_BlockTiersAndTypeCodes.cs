using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TechSpherex.CleanArchitecture.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BlockTiersAndTypeCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Align existing reefer containers with type code R (Appendix I)
            migrationBuilder.Sql("UPDATE \"Containers\" SET \"ContainerNumber\" = 'HMMR2468107' WHERE \"ContainerNumber\" = 'HMMU2468109';");
            migrationBuilder.Sql("UPDATE \"Containers\" SET \"ContainerNumber\" = 'HMMR3007774' WHERE \"ContainerNumber\" = 'HMMU3007776';");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Blocks_Dimensions",
                table: "Blocks");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Blocks_Dimensions",
                table: "Blocks",
                sql: "(\"IsVirtual\" AND \"MaxBays\" IS NULL AND \"MaxRows\" IS NULL AND \"MaxTiers\" IS NULL) OR (NOT \"IsVirtual\" AND \"MaxBays\" >= 1 AND \"MaxRows\" >= 1 AND \"MaxTiers\" >= 2)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Blocks_Dimensions",
                table: "Blocks");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Blocks_Dimensions",
                table: "Blocks",
                sql: "(\"IsVirtual\" AND \"MaxBays\" IS NULL AND \"MaxRows\" IS NULL AND \"MaxTiers\" IS NULL) OR (NOT \"IsVirtual\" AND \"MaxBays\" >= 1 AND \"MaxRows\" >= 1 AND \"MaxTiers\" >= 1)");

            // Restore the previous reefer container numbers
            migrationBuilder.Sql("UPDATE \"Containers\" SET \"ContainerNumber\" = 'HMMU2468109' WHERE \"ContainerNumber\" = 'HMMR2468107';");
            migrationBuilder.Sql("UPDATE \"Containers\" SET \"ContainerNumber\" = 'HMMU3007776' WHERE \"ContainerNumber\" = 'HMMR3007774';");
        }
    }
}
