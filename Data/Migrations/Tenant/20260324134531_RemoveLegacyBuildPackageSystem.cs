using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Opcentrix_V3.Data.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class RemoveLegacyBuildPackageSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQLite ignores DropForeignKey, so disable FK enforcement for the migration
            migrationBuilder.Sql("PRAGMA foreign_keys = OFF;");

            migrationBuilder.DropForeignKey(
                name: "FK_BuildTemplates_BuildPackages_SourceBuildPackageId",
                table: "BuildTemplates");

            migrationBuilder.DropForeignKey(
                name: "FK_PartInstances_BuildPackages_BuildPackageId",
                table: "PartInstances");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductionBatches_BuildPackages_OriginBuildPackageId",
                table: "ProductionBatches");

            migrationBuilder.DropForeignKey(
                name: "FK_StageExecutions_BuildPackages_BuildPackageId",
                table: "StageExecutions");

            // Drop FK columns first (clearing references before dropping parent tables)
            migrationBuilder.DropIndex(
                name: "IX_StageExecutions_BuildPackageId",
                table: "StageExecutions");

            migrationBuilder.DropIndex(
                name: "IX_ProductionBatches_OriginBuildPackageId",
                table: "ProductionBatches");

            migrationBuilder.DropIndex(
                name: "IX_PartInstances_BuildPackageId",
                table: "PartInstances");

            migrationBuilder.DropIndex(
                name: "IX_BuildTemplates_SourceBuildPackageId",
                table: "BuildTemplates");

            migrationBuilder.DropColumn(
                name: "BuildPackageId",
                table: "StageExecutions");

            migrationBuilder.DropColumn(
                name: "OriginBuildPackageId",
                table: "ProductionBatches");

            migrationBuilder.DropColumn(
                name: "BuildPackageId",
                table: "PartInstances");

            migrationBuilder.DropColumn(
                name: "SourceBuildPackageId",
                table: "BuildTemplates");

            // Now safe to drop the parent tables
            migrationBuilder.DropTable(
                name: "BuildFileInfos");

            migrationBuilder.DropTable(
                name: "BuildPackageParts");

            migrationBuilder.DropTable(
                name: "BuildPackageRevisions");

            migrationBuilder.DropTable(
                name: "BuildPackages");

            migrationBuilder.AddColumn<string>(
                name: "BuildPurpose",
                table: "MachinePrograms",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.Sql("PRAGMA foreign_keys = ON;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BuildPurpose",
                table: "MachinePrograms");

            migrationBuilder.AddColumn<int>(
                name: "BuildPackageId",
                table: "StageExecutions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OriginBuildPackageId",
                table: "ProductionBatches",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BuildPackageId",
                table: "PartInstances",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceBuildPackageId",
                table: "BuildTemplates",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BuildPackages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BuildTemplateId = table.Column<int>(type: "INTEGER", nullable: true),
                    MachineId = table.Column<int>(type: "INTEGER", nullable: true),
                    PredecessorBuildPackageId = table.Column<int>(type: "INTEGER", nullable: true),
                    ScheduledJobId = table.Column<int>(type: "INTEGER", nullable: true),
                    SourceBuildPackageId = table.Column<int>(type: "INTEGER", nullable: true),
                    BuildParameters = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CurrentRevision = table.Column<int>(type: "INTEGER", nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    EstimatedDurationHours = table.Column<double>(type: "REAL", nullable: true),
                    IsLocked = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsSlicerDataEntered = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Material = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    PlateReleasedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PrintCompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PrintStartedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ScheduledDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BuildPackages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BuildPackages_BuildPackages_PredecessorBuildPackageId",
                        column: x => x.PredecessorBuildPackageId,
                        principalTable: "BuildPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_BuildPackages_BuildPackages_SourceBuildPackageId",
                        column: x => x.SourceBuildPackageId,
                        principalTable: "BuildPackages",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BuildPackages_BuildTemplates_BuildTemplateId",
                        column: x => x.BuildTemplateId,
                        principalTable: "BuildTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_BuildPackages_Jobs_ScheduledJobId",
                        column: x => x.ScheduledJobId,
                        principalTable: "Jobs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BuildPackages_Machines_MachineId",
                        column: x => x.MachineId,
                        principalTable: "Machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "BuildFileInfos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BuildPackageId = table.Column<int>(type: "INTEGER", nullable: false),
                    BuildHeightMm = table.Column<decimal>(type: "TEXT", nullable: true),
                    EstimatedPowderKg = table.Column<decimal>(type: "TEXT", nullable: true),
                    EstimatedPrintTimeHours = table.Column<decimal>(type: "TEXT", nullable: true),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ImportedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ImportedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LayerCount = table.Column<int>(type: "INTEGER", nullable: true),
                    PartPositionsJson = table.Column<string>(type: "TEXT", nullable: true),
                    SlicerSoftware = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    SlicerVersion = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BuildFileInfos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BuildFileInfos_BuildPackages_BuildPackageId",
                        column: x => x.BuildPackageId,
                        principalTable: "BuildPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BuildPackageParts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BuildPackageId = table.Column<int>(type: "INTEGER", nullable: false),
                    PartId = table.Column<int>(type: "INTEGER", nullable: false),
                    WorkOrderLineId = table.Column<int>(type: "INTEGER", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    SlicerNotes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    StackLevel = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BuildPackageParts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BuildPackageParts_BuildPackages_BuildPackageId",
                        column: x => x.BuildPackageId,
                        principalTable: "BuildPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BuildPackageParts_Parts_PartId",
                        column: x => x.PartId,
                        principalTable: "Parts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BuildPackageParts_WorkOrderLines_WorkOrderLineId",
                        column: x => x.WorkOrderLineId,
                        principalTable: "WorkOrderLines",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "BuildPackageRevisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BuildPackageId = table.Column<int>(type: "INTEGER", nullable: false),
                    ChangeNotes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ChangedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ParametersSnapshotJson = table.Column<string>(type: "TEXT", nullable: true),
                    PartsSnapshotJson = table.Column<string>(type: "TEXT", nullable: false),
                    RevisionDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RevisionNumber = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BuildPackageRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BuildPackageRevisions_BuildPackages_BuildPackageId",
                        column: x => x.BuildPackageId,
                        principalTable: "BuildPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StageExecutions_BuildPackageId",
                table: "StageExecutions",
                column: "BuildPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionBatches_OriginBuildPackageId",
                table: "ProductionBatches",
                column: "OriginBuildPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_PartInstances_BuildPackageId",
                table: "PartInstances",
                column: "BuildPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_BuildTemplates_SourceBuildPackageId",
                table: "BuildTemplates",
                column: "SourceBuildPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_BuildFileInfos_BuildPackageId",
                table: "BuildFileInfos",
                column: "BuildPackageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BuildPackageParts_BuildPackageId",
                table: "BuildPackageParts",
                column: "BuildPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_BuildPackageParts_PartId",
                table: "BuildPackageParts",
                column: "PartId");

            migrationBuilder.CreateIndex(
                name: "IX_BuildPackageParts_WorkOrderLineId",
                table: "BuildPackageParts",
                column: "WorkOrderLineId");

            migrationBuilder.CreateIndex(
                name: "IX_BuildPackageRevisions_BuildPackageId",
                table: "BuildPackageRevisions",
                column: "BuildPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_BuildPackages_BuildTemplateId",
                table: "BuildPackages",
                column: "BuildTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_BuildPackages_MachineId",
                table: "BuildPackages",
                column: "MachineId");

            migrationBuilder.CreateIndex(
                name: "IX_BuildPackages_PredecessorBuildPackageId",
                table: "BuildPackages",
                column: "PredecessorBuildPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_BuildPackages_ScheduledJobId",
                table: "BuildPackages",
                column: "ScheduledJobId");

            migrationBuilder.CreateIndex(
                name: "IX_BuildPackages_SourceBuildPackageId",
                table: "BuildPackages",
                column: "SourceBuildPackageId");

            migrationBuilder.AddForeignKey(
                name: "FK_BuildTemplates_BuildPackages_SourceBuildPackageId",
                table: "BuildTemplates",
                column: "SourceBuildPackageId",
                principalTable: "BuildPackages",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PartInstances_BuildPackages_BuildPackageId",
                table: "PartInstances",
                column: "BuildPackageId",
                principalTable: "BuildPackages",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductionBatches_BuildPackages_OriginBuildPackageId",
                table: "ProductionBatches",
                column: "OriginBuildPackageId",
                principalTable: "BuildPackages",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_StageExecutions_BuildPackages_BuildPackageId",
                table: "StageExecutions",
                column: "BuildPackageId",
                principalTable: "BuildPackages",
                principalColumn: "Id");
        }
    }
}
