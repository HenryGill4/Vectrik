using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vectrik.Data.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddProgramTypeAndProgramParts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "BuildHeightMm",
                table: "MachinePrograms",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "EstimatedPowderKg",
                table: "MachinePrograms",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "EstimatedPrintHours",
                table: "MachinePrograms",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LayerCount",
                table: "MachinePrograms",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaterialId",
                table: "MachinePrograms",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PartPositionsJson",
                table: "MachinePrograms",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProgramType",
                table: "MachinePrograms",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SlicerFileName",
                table: "MachinePrograms",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SlicerSoftware",
                table: "MachinePrograms",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SlicerVersion",
                table: "MachinePrograms",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProgramParts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MachineProgramId = table.Column<int>(type: "INTEGER", nullable: false),
                    PartId = table.Column<int>(type: "INTEGER", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    StackLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    PositionNotes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    WorkOrderLineId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgramParts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProgramParts_MachinePrograms_MachineProgramId",
                        column: x => x.MachineProgramId,
                        principalTable: "MachinePrograms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProgramParts_Parts_PartId",
                        column: x => x.PartId,
                        principalTable: "Parts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProgramParts_WorkOrderLines_WorkOrderLineId",
                        column: x => x.WorkOrderLineId,
                        principalTable: "WorkOrderLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MachinePrograms_MaterialId",
                table: "MachinePrograms",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramParts_MachineProgramId_PartId",
                table: "ProgramParts",
                columns: new[] { "MachineProgramId", "PartId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProgramParts_PartId",
                table: "ProgramParts",
                column: "PartId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramParts_WorkOrderLineId",
                table: "ProgramParts",
                column: "WorkOrderLineId");

            migrationBuilder.AddForeignKey(
                name: "FK_MachinePrograms_Materials_MaterialId",
                table: "MachinePrograms",
                column: "MaterialId",
                principalTable: "Materials",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MachinePrograms_Materials_MaterialId",
                table: "MachinePrograms");

            migrationBuilder.DropTable(
                name: "ProgramParts");

            migrationBuilder.DropIndex(
                name: "IX_MachinePrograms_MaterialId",
                table: "MachinePrograms");

            migrationBuilder.DropColumn(
                name: "BuildHeightMm",
                table: "MachinePrograms");

            migrationBuilder.DropColumn(
                name: "EstimatedPowderKg",
                table: "MachinePrograms");

            migrationBuilder.DropColumn(
                name: "EstimatedPrintHours",
                table: "MachinePrograms");

            migrationBuilder.DropColumn(
                name: "LayerCount",
                table: "MachinePrograms");

            migrationBuilder.DropColumn(
                name: "MaterialId",
                table: "MachinePrograms");

            migrationBuilder.DropColumn(
                name: "PartPositionsJson",
                table: "MachinePrograms");

            migrationBuilder.DropColumn(
                name: "ProgramType",
                table: "MachinePrograms");

            migrationBuilder.DropColumn(
                name: "SlicerFileName",
                table: "MachinePrograms");

            migrationBuilder.DropColumn(
                name: "SlicerSoftware",
                table: "MachinePrograms");

            migrationBuilder.DropColumn(
                name: "SlicerVersion",
                table: "MachinePrograms");
        }
    }
}
