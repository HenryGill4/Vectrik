using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Opcentrix_V3.Data.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class RemoveBuildJobAndAddTemplateLineage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DelayLogs_BuildJobs_BuildJobId",
                table: "DelayLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_QCInspections_BuildJobs_BuildJobId",
                table: "QCInspections");

            migrationBuilder.DropTable(
                name: "BuildJobParts");

            migrationBuilder.DropTable(
                name: "BuildJobs");

            migrationBuilder.DropIndex(
                name: "IX_QCInspections_BuildJobId",
                table: "QCInspections");

            migrationBuilder.DropIndex(
                name: "IX_DelayLogs_BuildJobId",
                table: "DelayLogs");

            migrationBuilder.DropColumn(
                name: "BuildJobId",
                table: "QCInspections");

            migrationBuilder.DropColumn(
                name: "BuildJobId",
                table: "DelayLogs");

            migrationBuilder.AddColumn<int>(
                name: "SourceTemplateId",
                table: "MachinePrograms",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MachinePrograms_SourceTemplateId",
                table: "MachinePrograms",
                column: "SourceTemplateId");

            migrationBuilder.AddForeignKey(
                name: "FK_MachinePrograms_BuildTemplates_SourceTemplateId",
                table: "MachinePrograms",
                column: "SourceTemplateId",
                principalTable: "BuildTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MachinePrograms_BuildTemplates_SourceTemplateId",
                table: "MachinePrograms");

            migrationBuilder.DropIndex(
                name: "IX_MachinePrograms_SourceTemplateId",
                table: "MachinePrograms");

            migrationBuilder.DropColumn(
                name: "SourceTemplateId",
                table: "MachinePrograms");

            migrationBuilder.AddColumn<int>(
                name: "BuildJobId",
                table: "QCInspections",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BuildJobId",
                table: "DelayLogs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BuildJobs",
                columns: table => new
                {
                    BuildId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    JobId = table.Column<int>(type: "INTEGER", nullable: true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    ActualEndTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ActualStartTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndReason = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    GasUsedLiters = table.Column<float>(type: "REAL", nullable: true),
                    LaserRunTime = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Material = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    OperatorActualHours = table.Column<decimal>(type: "TEXT", nullable: true),
                    OperatorEstimatedHours = table.Column<decimal>(type: "TEXT", nullable: true),
                    PowderUsedLiters = table.Column<float>(type: "REAL", nullable: true),
                    PrinterName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ScheduledEndTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ScheduledStartTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalPartsInBuild = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BuildJobs", x => x.BuildId);
                    table.ForeignKey(
                        name: "FK_BuildJobs_Jobs_JobId",
                        column: x => x.JobId,
                        principalTable: "Jobs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BuildJobs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BuildJobParts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BuildJobId = table.Column<int>(type: "INTEGER", nullable: false),
                    PartId = table.Column<int>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    PartNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BuildJobParts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BuildJobParts_BuildJobs_BuildJobId",
                        column: x => x.BuildJobId,
                        principalTable: "BuildJobs",
                        principalColumn: "BuildId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BuildJobParts_Parts_PartId",
                        column: x => x.PartId,
                        principalTable: "Parts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QCInspections_BuildJobId",
                table: "QCInspections",
                column: "BuildJobId");

            migrationBuilder.CreateIndex(
                name: "IX_DelayLogs_BuildJobId",
                table: "DelayLogs",
                column: "BuildJobId");

            migrationBuilder.CreateIndex(
                name: "IX_BuildJobParts_BuildJobId",
                table: "BuildJobParts",
                column: "BuildJobId");

            migrationBuilder.CreateIndex(
                name: "IX_BuildJobParts_PartId",
                table: "BuildJobParts",
                column: "PartId");

            migrationBuilder.CreateIndex(
                name: "IX_BuildJobs_JobId",
                table: "BuildJobs",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_BuildJobs_UserId",
                table: "BuildJobs",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_DelayLogs_BuildJobs_BuildJobId",
                table: "DelayLogs",
                column: "BuildJobId",
                principalTable: "BuildJobs",
                principalColumn: "BuildId");

            migrationBuilder.AddForeignKey(
                name: "FK_QCInspections_BuildJobs_BuildJobId",
                table: "QCInspections",
                column: "BuildJobId",
                principalTable: "BuildJobs",
                principalColumn: "BuildId");
        }
    }
}
