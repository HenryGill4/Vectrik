using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vectrik.Data.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class UnifyMachineIdToIntFK : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Convert existing string MachineId values to int FK via Machines table join
            // Must run BEFORE column type change so the data is already numeric
            migrationBuilder.Sql(
                "UPDATE Jobs SET MachineId = (SELECT m.Id FROM Machines m WHERE m.MachineId = Jobs.MachineId) WHERE MachineId IS NOT NULL;");
            migrationBuilder.Sql(
                "UPDATE BuildPackages SET MachineId = (SELECT m.Id FROM Machines m WHERE m.MachineId = BuildPackages.MachineId) WHERE MachineId IS NOT NULL;");

            migrationBuilder.AlterColumn<int>(
                name: "MachineId",
                table: "Jobs",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "MachineId",
                table: "BuildPackages",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 50);

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_MachineId",
                table: "Jobs",
                column: "MachineId");

            migrationBuilder.CreateIndex(
                name: "IX_BuildPackages_MachineId",
                table: "BuildPackages",
                column: "MachineId");

            migrationBuilder.AddForeignKey(
                name: "FK_BuildPackages_Machines_MachineId",
                table: "BuildPackages",
                column: "MachineId",
                principalTable: "Machines",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Jobs_Machines_MachineId",
                table: "Jobs",
                column: "MachineId",
                principalTable: "Machines",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BuildPackages_Machines_MachineId",
                table: "BuildPackages");

            migrationBuilder.DropForeignKey(
                name: "FK_Jobs_Machines_MachineId",
                table: "Jobs");

            migrationBuilder.DropIndex(
                name: "IX_Jobs_MachineId",
                table: "Jobs");

            migrationBuilder.DropIndex(
                name: "IX_BuildPackages_MachineId",
                table: "BuildPackages");

            migrationBuilder.AlterColumn<string>(
                name: "MachineId",
                table: "Jobs",
                type: "TEXT",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "MachineId",
                table: "BuildPackages",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            // Convert int FK back to string MachineId via Machines table join
            migrationBuilder.Sql(
                "UPDATE Jobs SET MachineId = (SELECT m.MachineId FROM Machines m WHERE m.Id = CAST(Jobs.MachineId AS INTEGER)) WHERE MachineId IS NOT NULL;");
            migrationBuilder.Sql(
                "UPDATE BuildPackages SET MachineId = (SELECT m.MachineId FROM Machines m WHERE m.Id = CAST(BuildPackages.MachineId AS INTEGER)) WHERE MachineId IS NOT NULL;");
        }
    }
}
