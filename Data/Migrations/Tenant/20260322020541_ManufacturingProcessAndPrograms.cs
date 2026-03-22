using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Opcentrix_V3.Data.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class ManufacturingProcessAndPrograms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MachineProgramId",
                table: "ProcessStages",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MachinePrograms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PartId = table.Column<int>(type: "INTEGER", nullable: true),
                    MachineId = table.Column<int>(type: "INTEGER", nullable: true),
                    MachineType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    ProcessStageId = table.Column<int>(type: "INTEGER", nullable: true),
                    ProgramNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    SetupTimeMinutes = table.Column<double>(type: "REAL", nullable: true),
                    RunTimeMinutes = table.Column<double>(type: "REAL", nullable: true),
                    CycleTimeMinutes = table.Column<double>(type: "REAL", nullable: true),
                    ToolingRequired = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    FixtureRequired = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Parameters = table.Column<string>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MachinePrograms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MachinePrograms_Machines_MachineId",
                        column: x => x.MachineId,
                        principalTable: "Machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MachinePrograms_Parts_PartId",
                        column: x => x.PartId,
                        principalTable: "Parts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MachinePrograms_ProcessStages_ProcessStageId",
                        column: x => x.ProcessStageId,
                        principalTable: "ProcessStages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "MachineProgramFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MachineProgramId = table.Column<int>(type: "INTEGER", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    FileType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    FileHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    IsPrimary = table.Column<bool>(type: "INTEGER", nullable: false),
                    UploadedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    UploadedDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MachineProgramFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MachineProgramFiles_MachinePrograms_MachineProgramId",
                        column: x => x.MachineProgramId,
                        principalTable: "MachinePrograms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessStages_MachineProgramId",
                table: "ProcessStages",
                column: "MachineProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_MachineProgramFiles_MachineProgramId",
                table: "MachineProgramFiles",
                column: "MachineProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_MachinePrograms_MachineId",
                table: "MachinePrograms",
                column: "MachineId");

            migrationBuilder.CreateIndex(
                name: "IX_MachinePrograms_PartId",
                table: "MachinePrograms",
                column: "PartId");

            migrationBuilder.CreateIndex(
                name: "IX_MachinePrograms_ProcessStageId",
                table: "MachinePrograms",
                column: "ProcessStageId");

            migrationBuilder.CreateIndex(
                name: "IX_MachinePrograms_ProgramNumber",
                table: "MachinePrograms",
                column: "ProgramNumber");

            migrationBuilder.AddForeignKey(
                name: "FK_ProcessStages_MachinePrograms_MachineProgramId",
                table: "ProcessStages",
                column: "MachineProgramId",
                principalTable: "MachinePrograms",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProcessStages_MachinePrograms_MachineProgramId",
                table: "ProcessStages");

            migrationBuilder.DropTable(
                name: "MachineProgramFiles");

            migrationBuilder.DropTable(
                name: "MachinePrograms");

            migrationBuilder.DropIndex(
                name: "IX_ProcessStages_MachineProgramId",
                table: "ProcessStages");

            migrationBuilder.DropColumn(
                name: "MachineProgramId",
                table: "ProcessStages");
        }
    }
}
