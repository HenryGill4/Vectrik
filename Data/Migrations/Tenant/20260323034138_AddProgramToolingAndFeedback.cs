using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vectrik.Data.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddProgramToolingAndFeedback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProgramFeedbacks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MachineProgramId = table.Column<int>(type: "INTEGER", nullable: false),
                    StageExecutionId = table.Column<int>(type: "INTEGER", nullable: true),
                    OperatorUserId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    OperatorName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Category = table.Column<int>(type: "INTEGER", nullable: false),
                    Severity = table.Column<int>(type: "INTEGER", nullable: false),
                    Comment = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ReviewedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Resolution = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgramFeedbacks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProgramFeedbacks_MachinePrograms_MachineProgramId",
                        column: x => x.MachineProgramId,
                        principalTable: "MachinePrograms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProgramFeedbacks_StageExecutions_StageExecutionId",
                        column: x => x.StageExecutionId,
                        principalTable: "StageExecutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ProgramToolingItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MachineProgramId = table.Column<int>(type: "INTEGER", nullable: false),
                    ToolPosition = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    MachineComponentId = table.Column<int>(type: "INTEGER", nullable: true),
                    IsFixture = table.Column<bool>(type: "INTEGER", nullable: false),
                    WearLifeHours = table.Column<double>(type: "REAL", nullable: true),
                    WearLifeBuilds = table.Column<int>(type: "INTEGER", nullable: true),
                    WarningThresholdPercent = table.Column<int>(type: "INTEGER", nullable: false),
                    SparePartNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    LastModifiedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgramToolingItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProgramToolingItems_MachineComponents_MachineComponentId",
                        column: x => x.MachineComponentId,
                        principalTable: "MachineComponents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProgramToolingItems_MachinePrograms_MachineProgramId",
                        column: x => x.MachineProgramId,
                        principalTable: "MachinePrograms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProgramFeedbacks_MachineProgramId_Status",
                table: "ProgramFeedbacks",
                columns: new[] { "MachineProgramId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProgramFeedbacks_StageExecutionId",
                table: "ProgramFeedbacks",
                column: "StageExecutionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramToolingItems_MachineComponentId",
                table: "ProgramToolingItems",
                column: "MachineComponentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramToolingItems_MachineProgramId_ToolPosition",
                table: "ProgramToolingItems",
                columns: new[] { "MachineProgramId", "ToolPosition" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProgramFeedbacks");

            migrationBuilder.DropTable(
                name: "ProgramToolingItems");
        }
    }
}
