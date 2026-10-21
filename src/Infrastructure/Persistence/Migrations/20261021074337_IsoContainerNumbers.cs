using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TechSpherex.CleanArchitecture.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IsoContainerNumbers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ISO 6346: every freight container uses U as the 4th letter (the type is in the ISO code)
            migrationBuilder.Sql("UPDATE \"Containers\" SET \"ContainerNumber\" = 'HMMU2468109' WHERE \"ContainerNumber\" = 'HMMR2468107';");
            migrationBuilder.Sql("UPDATE \"Containers\" SET \"ContainerNumber\" = 'HMMU3007776' WHERE \"ContainerNumber\" = 'HMMR3007774';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore the Appendix I type code R numbers
            migrationBuilder.Sql("UPDATE \"Containers\" SET \"ContainerNumber\" = 'HMMR2468107' WHERE \"ContainerNumber\" = 'HMMU2468109';");
            migrationBuilder.Sql("UPDATE \"Containers\" SET \"ContainerNumber\" = 'HMMR3007774' WHERE \"ContainerNumber\" = 'HMMU3007776';");
        }
    }
}
