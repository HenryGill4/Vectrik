using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vectrik.Data.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddWorkInstructionToMachineProgram : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "WorkInstructionId",
                table: "MachinePrograms",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MachinePrograms_WorkInstructionId",
                table: "MachinePrograms",
                column: "WorkInstructionId");

            migrationBuilder.AddForeignKey(
                name: "FK_MachinePrograms_WorkInstructions_WorkInstructionId",
                table: "MachinePrograms",
                column: "WorkInstructionId",
                principalTable: "WorkInstructions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MachinePrograms_WorkInstructions_WorkInstructionId",
                table: "MachinePrograms");

            migrationBuilder.DropIndex(
                name: "IX_MachinePrograms_WorkInstructionId",
                table: "MachinePrograms");

            migrationBuilder.DropColumn(
                name: "WorkInstructionId",
                table: "MachinePrograms");
        }
    }
}
