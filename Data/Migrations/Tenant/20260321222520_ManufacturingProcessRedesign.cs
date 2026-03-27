using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vectrik.Data.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class ManufacturingProcessRedesign : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BatchCapacity",
                table: "ProductionStages");

            migrationBuilder.DropColumn(
                name: "IsBatchStage",
                table: "ProductionStages");

            migrationBuilder.DropColumn(
                name: "IsBuildLevelStage",
                table: "ProductionStages");

            migrationBuilder.DropColumn(
                name: "TriggerPlateRelease",
                table: "ProductionStages");

            migrationBuilder.DropColumn(
                name: "DepowderingDurationHours",
                table: "PartAdditiveBuildConfigs");

            migrationBuilder.DropColumn(
                name: "DepowderingPartsPerBatch",
                table: "PartAdditiveBuildConfigs");

            migrationBuilder.DropColumn(
                name: "HeatTreatmentDurationHours",
                table: "PartAdditiveBuildConfigs");

            migrationBuilder.DropColumn(
                name: "HeatTreatmentPartsPerBatch",
                table: "PartAdditiveBuildConfigs");

            migrationBuilder.DropColumn(
                name: "WireEdmDurationHours",
                table: "PartAdditiveBuildConfigs");

            migrationBuilder.DropColumn(
                name: "WireEdmPartsPerSession",
                table: "PartAdditiveBuildConfigs");

            migrationBuilder.DropColumn(
                name: "DepowderingHours",
                table: "BuildPackages");

            migrationBuilder.DropColumn(
                name: "HeatTreatmentHours",
                table: "BuildPackages");

            migrationBuilder.DropColumn(
                name: "WireEdmHours",
                table: "BuildPackages");

            migrationBuilder.RenameColumn(
                name: "HasPostPrintBatching",
                table: "ManufacturingApproaches",
                newName: "DefaultBatchCapacity");

            migrationBuilder.AddColumn<int>(
                name: "ProcessStageId",
                table: "StageExecutions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProductionBatchId",
                table: "StageExecutions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrentBatchId",
                table: "PartInstances",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ManufacturingProcessId",
                table: "Jobs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProductionBatchId",
                table: "Jobs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Scope",
                table: "Jobs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "BatchPartAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProductionBatchId = table.Column<int>(type: "INTEGER", nullable: false),
                    PartInstanceId = table.Column<int>(type: "INTEGER", nullable: false),
                    Action = table.Column<int>(type: "INTEGER", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    AtProcessStageId = table.Column<int>(type: "INTEGER", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PerformedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BatchPartAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BatchPartAssignments_PartInstances_PartInstanceId",
                        column: x => x.PartInstanceId,
                        principalTable: "PartInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ManufacturingProcesses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PartId = table.Column<int>(type: "INTEGER", nullable: false),
                    ManufacturingApproachId = table.Column<int>(type: "INTEGER", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    PlateReleaseStageId = table.Column<int>(type: "INTEGER", nullable: true),
                    DefaultBatchCapacity = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManufacturingProcesses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ManufacturingProcesses_ManufacturingApproaches_ManufacturingApproachId",
                        column: x => x.ManufacturingApproachId,
                        principalTable: "ManufacturingApproaches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ManufacturingProcesses_Parts_PartId",
                        column: x => x.PartId,
                        principalTable: "Parts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProcessStages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ManufacturingProcessId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductionStageId = table.Column<int>(type: "INTEGER", nullable: false),
                    ExecutionOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    ProcessingLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    SetupDurationMode = table.Column<int>(type: "INTEGER", nullable: false),
                    SetupTimeMinutes = table.Column<double>(type: "REAL", nullable: true),
                    RunDurationMode = table.Column<int>(type: "INTEGER", nullable: false),
                    RunTimeMinutes = table.Column<double>(type: "REAL", nullable: true),
                    DurationFromBuildConfig = table.Column<bool>(type: "INTEGER", nullable: false),
                    BatchCapacityOverride = table.Column<int>(type: "INTEGER", nullable: true),
                    AllowRebatching = table.Column<bool>(type: "INTEGER", nullable: false),
                    ConsolidateBatchesAtStage = table.Column<bool>(type: "INTEGER", nullable: false),
                    AssignedMachineId = table.Column<int>(type: "INTEGER", nullable: true),
                    RequiresSpecificMachine = table.Column<bool>(type: "INTEGER", nullable: false),
                    PreferredMachineIds = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    HourlyRateOverride = table.Column<decimal>(type: "decimal(8,2)", nullable: true),
                    MaterialCost = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    IsRequired = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsBlocking = table.Column<bool>(type: "INTEGER", nullable: false),
                    AllowParallelExecution = table.Column<bool>(type: "INTEGER", nullable: false),
                    AllowSkip = table.Column<bool>(type: "INTEGER", nullable: false),
                    RequiresQualityCheck = table.Column<bool>(type: "INTEGER", nullable: false),
                    RequiresSerialNumber = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsExternalOperation = table.Column<bool>(type: "INTEGER", nullable: false),
                    ExternalTurnaroundDays = table.Column<double>(type: "REAL", nullable: true),
                    StageParameters = table.Column<string>(type: "TEXT", nullable: true),
                    RequiredMaterials = table.Column<string>(type: "TEXT", nullable: true),
                    RequiredTooling = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    QualityRequirements = table.Column<string>(type: "TEXT", nullable: true),
                    SpecialInstructions = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    ActualAverageDurationMinutes = table.Column<double>(type: "REAL", nullable: true),
                    ActualSampleCount = table.Column<int>(type: "INTEGER", nullable: true),
                    EstimateSource = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    LastModifiedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessStages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcessStages_Machines_AssignedMachineId",
                        column: x => x.AssignedMachineId,
                        principalTable: "Machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProcessStages_ManufacturingProcesses_ManufacturingProcessId",
                        column: x => x.ManufacturingProcessId,
                        principalTable: "ManufacturingProcesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProcessStages_ProductionStages_ProductionStageId",
                        column: x => x.ProductionStageId,
                        principalTable: "ProductionStages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductionBatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BatchNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    OriginBuildPackageId = table.Column<int>(type: "INTEGER", nullable: true),
                    ContainerLabel = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Capacity = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentPartCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentProcessStageId = table.Column<int>(type: "INTEGER", nullable: true),
                    AssignedMachineId = table.Column<int>(type: "INTEGER", nullable: true),
                    StageExecutionId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductionBatches_BuildPackages_OriginBuildPackageId",
                        column: x => x.OriginBuildPackageId,
                        principalTable: "BuildPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProductionBatches_Machines_AssignedMachineId",
                        column: x => x.AssignedMachineId,
                        principalTable: "Machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProductionBatches_ProcessStages_CurrentProcessStageId",
                        column: x => x.CurrentProcessStageId,
                        principalTable: "ProcessStages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProductionBatches_StageExecutions_StageExecutionId",
                        column: x => x.StageExecutionId,
                        principalTable: "StageExecutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StageExecutions_ProcessStageId",
                table: "StageExecutions",
                column: "ProcessStageId");

            migrationBuilder.CreateIndex(
                name: "IX_StageExecutions_ProductionBatchId",
                table: "StageExecutions",
                column: "ProductionBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_PartInstances_CurrentBatchId",
                table: "PartInstances",
                column: "CurrentBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_ManufacturingProcessId",
                table: "Jobs",
                column: "ManufacturingProcessId");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_ProductionBatchId",
                table: "Jobs",
                column: "ProductionBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_BatchPartAssignments_AtProcessStageId",
                table: "BatchPartAssignments",
                column: "AtProcessStageId");

            migrationBuilder.CreateIndex(
                name: "IX_BatchPartAssignments_PartInstanceId_Timestamp",
                table: "BatchPartAssignments",
                columns: new[] { "PartInstanceId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_BatchPartAssignments_ProductionBatchId",
                table: "BatchPartAssignments",
                column: "ProductionBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_ManufacturingProcesses_ManufacturingApproachId",
                table: "ManufacturingProcesses",
                column: "ManufacturingApproachId");

            migrationBuilder.CreateIndex(
                name: "IX_ManufacturingProcesses_PartId",
                table: "ManufacturingProcesses",
                column: "PartId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ManufacturingProcesses_PlateReleaseStageId",
                table: "ManufacturingProcesses",
                column: "PlateReleaseStageId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessStages_AssignedMachineId",
                table: "ProcessStages",
                column: "AssignedMachineId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessStages_ManufacturingProcessId_ExecutionOrder",
                table: "ProcessStages",
                columns: new[] { "ManufacturingProcessId", "ExecutionOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessStages_ProductionStageId",
                table: "ProcessStages",
                column: "ProductionStageId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionBatches_AssignedMachineId",
                table: "ProductionBatches",
                column: "AssignedMachineId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionBatches_BatchNumber",
                table: "ProductionBatches",
                column: "BatchNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionBatches_CurrentProcessStageId",
                table: "ProductionBatches",
                column: "CurrentProcessStageId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionBatches_OriginBuildPackageId",
                table: "ProductionBatches",
                column: "OriginBuildPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionBatches_StageExecutionId",
                table: "ProductionBatches",
                column: "StageExecutionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Jobs_ManufacturingProcesses_ManufacturingProcessId",
                table: "Jobs",
                column: "ManufacturingProcessId",
                principalTable: "ManufacturingProcesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Jobs_ProductionBatches_ProductionBatchId",
                table: "Jobs",
                column: "ProductionBatchId",
                principalTable: "ProductionBatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PartInstances_ProductionBatches_CurrentBatchId",
                table: "PartInstances",
                column: "CurrentBatchId",
                principalTable: "ProductionBatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_StageExecutions_ProcessStages_ProcessStageId",
                table: "StageExecutions",
                column: "ProcessStageId",
                principalTable: "ProcessStages",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_StageExecutions_ProductionBatches_ProductionBatchId",
                table: "StageExecutions",
                column: "ProductionBatchId",
                principalTable: "ProductionBatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_BatchPartAssignments_ProcessStages_AtProcessStageId",
                table: "BatchPartAssignments",
                column: "AtProcessStageId",
                principalTable: "ProcessStages",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_BatchPartAssignments_ProductionBatches_ProductionBatchId",
                table: "BatchPartAssignments",
                column: "ProductionBatchId",
                principalTable: "ProductionBatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ManufacturingProcesses_ProcessStages_PlateReleaseStageId",
                table: "ManufacturingProcesses",
                column: "PlateReleaseStageId",
                principalTable: "ProcessStages",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Jobs_ManufacturingProcesses_ManufacturingProcessId",
                table: "Jobs");

            migrationBuilder.DropForeignKey(
                name: "FK_Jobs_ProductionBatches_ProductionBatchId",
                table: "Jobs");

            migrationBuilder.DropForeignKey(
                name: "FK_PartInstances_ProductionBatches_CurrentBatchId",
                table: "PartInstances");

            migrationBuilder.DropForeignKey(
                name: "FK_StageExecutions_ProcessStages_ProcessStageId",
                table: "StageExecutions");

            migrationBuilder.DropForeignKey(
                name: "FK_StageExecutions_ProductionBatches_ProductionBatchId",
                table: "StageExecutions");

            migrationBuilder.DropForeignKey(
                name: "FK_ManufacturingProcesses_ProcessStages_PlateReleaseStageId",
                table: "ManufacturingProcesses");

            migrationBuilder.DropTable(
                name: "BatchPartAssignments");

            migrationBuilder.DropTable(
                name: "ProductionBatches");

            migrationBuilder.DropTable(
                name: "ProcessStages");

            migrationBuilder.DropTable(
                name: "ManufacturingProcesses");

            migrationBuilder.DropIndex(
                name: "IX_StageExecutions_ProcessStageId",
                table: "StageExecutions");

            migrationBuilder.DropIndex(
                name: "IX_StageExecutions_ProductionBatchId",
                table: "StageExecutions");

            migrationBuilder.DropIndex(
                name: "IX_PartInstances_CurrentBatchId",
                table: "PartInstances");

            migrationBuilder.DropIndex(
                name: "IX_Jobs_ManufacturingProcessId",
                table: "Jobs");

            migrationBuilder.DropIndex(
                name: "IX_Jobs_ProductionBatchId",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "ProcessStageId",
                table: "StageExecutions");

            migrationBuilder.DropColumn(
                name: "ProductionBatchId",
                table: "StageExecutions");

            migrationBuilder.DropColumn(
                name: "CurrentBatchId",
                table: "PartInstances");

            migrationBuilder.DropColumn(
                name: "ManufacturingProcessId",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "ProductionBatchId",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "Scope",
                table: "Jobs");

            migrationBuilder.RenameColumn(
                name: "DefaultBatchCapacity",
                table: "ManufacturingApproaches",
                newName: "HasPostPrintBatching");

            migrationBuilder.AddColumn<int>(
                name: "BatchCapacity",
                table: "ProductionStages",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsBatchStage",
                table: "ProductionStages",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsBuildLevelStage",
                table: "ProductionStages",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "TriggerPlateRelease",
                table: "ProductionStages",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<double>(
                name: "DepowderingDurationHours",
                table: "PartAdditiveBuildConfigs",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DepowderingPartsPerBatch",
                table: "PartAdditiveBuildConfigs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "HeatTreatmentDurationHours",
                table: "PartAdditiveBuildConfigs",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HeatTreatmentPartsPerBatch",
                table: "PartAdditiveBuildConfigs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "WireEdmDurationHours",
                table: "PartAdditiveBuildConfigs",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WireEdmPartsPerSession",
                table: "PartAdditiveBuildConfigs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "DepowderingHours",
                table: "BuildPackages",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "HeatTreatmentHours",
                table: "BuildPackages",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "WireEdmHours",
                table: "BuildPackages",
                type: "REAL",
                nullable: true);
        }
    }
}
