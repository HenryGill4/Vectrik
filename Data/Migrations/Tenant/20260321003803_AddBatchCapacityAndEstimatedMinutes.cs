using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vectrik.Data.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddBatchCapacityAndEstimatedMinutes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EstimatedHours",
                table: "PartStageRequirements");

            migrationBuilder.AddColumn<int>(
                name: "BatchCapacity",
                table: "ProductionStages",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "EstimatedMinutes",
                table: "PartStageRequirements",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BatchCapacity",
                table: "ProductionStages");

            migrationBuilder.DropColumn(
                name: "EstimatedMinutes",
                table: "PartStageRequirements");

            migrationBuilder.AddColumn<double>(
                name: "EstimatedHours",
                table: "PartStageRequirements",
                type: "REAL",
                nullable: true);
        }
    }
}
