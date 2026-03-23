using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Opcentrix_V3.Data.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddMachineProgramIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MachineProgramId",
                table: "StageExecutions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ActualAverageDurationMinutes",
                table: "MachinePrograms",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ActualSampleCount",
                table: "MachinePrograms",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EstimateSource",
                table: "MachinePrograms",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TotalRunCount",
                table: "MachinePrograms",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_StageExecutions_MachineProgramId",
                table: "StageExecutions",
                column: "MachineProgramId");

            migrationBuilder.AddForeignKey(
                name: "FK_StageExecutions_MachinePrograms_MachineProgramId",
                table: "StageExecutions",
                column: "MachineProgramId",
                principalTable: "MachinePrograms",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StageExecutions_MachinePrograms_MachineProgramId",
                table: "StageExecutions");

            migrationBuilder.DropIndex(
                name: "IX_StageExecutions_MachineProgramId",
                table: "StageExecutions");

            migrationBuilder.DropColumn(
                name: "MachineProgramId",
                table: "StageExecutions");

            migrationBuilder.DropColumn(
                name: "ActualAverageDurationMinutes",
                table: "MachinePrograms");

            migrationBuilder.DropColumn(
                name: "ActualSampleCount",
                table: "MachinePrograms");

            migrationBuilder.DropColumn(
                name: "EstimateSource",
                table: "MachinePrograms");

            migrationBuilder.DropColumn(
                name: "TotalRunCount",
                table: "MachinePrograms");
        }
    }
}
