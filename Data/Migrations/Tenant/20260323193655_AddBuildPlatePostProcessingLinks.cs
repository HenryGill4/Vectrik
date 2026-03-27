using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vectrik.Data.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddBuildPlatePostProcessingLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DepowderProgramId",
                table: "MachinePrograms",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EdmProgramId",
                table: "MachinePrograms",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MachinePrograms_DepowderProgramId",
                table: "MachinePrograms",
                column: "DepowderProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_MachinePrograms_EdmProgramId",
                table: "MachinePrograms",
                column: "EdmProgramId");

            migrationBuilder.AddForeignKey(
                name: "FK_MachinePrograms_MachinePrograms_DepowderProgramId",
                table: "MachinePrograms",
                column: "DepowderProgramId",
                principalTable: "MachinePrograms",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_MachinePrograms_MachinePrograms_EdmProgramId",
                table: "MachinePrograms",
                column: "EdmProgramId",
                principalTable: "MachinePrograms",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MachinePrograms_MachinePrograms_DepowderProgramId",
                table: "MachinePrograms");

            migrationBuilder.DropForeignKey(
                name: "FK_MachinePrograms_MachinePrograms_EdmProgramId",
                table: "MachinePrograms");

            migrationBuilder.DropIndex(
                name: "IX_MachinePrograms_DepowderProgramId",
                table: "MachinePrograms");

            migrationBuilder.DropIndex(
                name: "IX_MachinePrograms_EdmProgramId",
                table: "MachinePrograms");

            migrationBuilder.DropColumn(
                name: "DepowderProgramId",
                table: "MachinePrograms");

            migrationBuilder.DropColumn(
                name: "EdmProgramId",
                table: "MachinePrograms");
        }
    }
}
