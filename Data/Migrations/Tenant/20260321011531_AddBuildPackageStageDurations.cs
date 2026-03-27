using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vectrik.Data.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddBuildPackageStageDurations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "DepowderingHours",
                table: "BuildPackages",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "HeatTreatmentHours",
                table: "BuildPackages",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "WireEdmHours",
                table: "BuildPackages",
                type: "REAL",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DepowderingHours",
                table: "BuildPackages");

            migrationBuilder.DropColumn(
                name: "HeatTreatmentHours",
                table: "BuildPackages");

            migrationBuilder.DropColumn(
                name: "WireEdmHours",
                table: "BuildPackages");
        }
    }
}
