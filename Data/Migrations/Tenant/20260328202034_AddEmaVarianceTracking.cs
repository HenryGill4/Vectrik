using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vectrik.Data.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddEmaVarianceTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "ActualVarianceMinutes",
                table: "ProcessStages",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ActualVarianceMinutes",
                table: "MachinePrograms",
                type: "REAL",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActualVarianceMinutes",
                table: "ProcessStages");

            migrationBuilder.DropColumn(
                name: "ActualVarianceMinutes",
                table: "MachinePrograms");
        }
    }
}
