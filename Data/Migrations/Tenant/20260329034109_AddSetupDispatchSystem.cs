using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vectrik.Data.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddSetupDispatchSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SetupDispatchId",
                table: "StageExecutions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrentProgramId",
                table: "Machines",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSetupChangeAt",
                table: "Machines",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SetupState",
                table: "Machines",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "ActualAverageSetupMinutes",
                table: "MachinePrograms",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SetupSampleCount",
                table: "MachinePrograms",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "SetupVarianceMinutes",
                table: "MachinePrograms",
                type: "REAL",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DispatchConfigurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MachineId = table.Column<int>(type: "INTEGER", nullable: true),
                    ProductionStageId = table.Column<int>(type: "INTEGER", nullable: true),
                    AutoDispatchEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    MaxQueueDepth = table.Column<int>(type: "INTEGER", nullable: false),
                    LookAheadHours = table.Column<int>(type: "INTEGER", nullable: false),
                    ChangeoverPenaltyWeight = table.Column<decimal>(type: "decimal(4,2)", nullable: false),
                    DueDateWeight = table.Column<decimal>(type: "decimal(4,2)", nullable: false),
                    ThroughputWeight = table.Column<decimal>(type: "decimal(4,2)", nullable: false),
                    MaintenanceBufferHours = table.Column<int>(type: "INTEGER", nullable: false),
                    RequiresVerification = table.Column<bool>(type: "INTEGER", nullable: false),
                    AutoAssignOperator = table.Column<bool>(type: "INTEGER", nullable: false),
                    NotifyOnDispatch = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DispatchConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DispatchConfigurations_Machines_MachineId",
                        column: x => x.MachineId,
                        principalTable: "Machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DispatchConfigurations_ProductionStages_ProductionStageId",
                        column: x => x.ProductionStageId,
                        principalTable: "ProductionStages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "OperatorSetupProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    MachineId = table.Column<int>(type: "INTEGER", nullable: false),
                    MachineProgramId = table.Column<int>(type: "INTEGER", nullable: true),
                    AverageSetupMinutes = table.Column<double>(type: "REAL", nullable: true),
                    SampleCount = table.Column<int>(type: "INTEGER", nullable: false),
                    VarianceMinutes = table.Column<double>(type: "REAL", nullable: true),
                    FastestSetupMinutes = table.Column<double>(type: "REAL", nullable: true),
                    ProficiencyLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    IsPreferred = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperatorSetupProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperatorSetupProfiles_MachinePrograms_MachineProgramId",
                        column: x => x.MachineProgramId,
                        principalTable: "MachinePrograms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_OperatorSetupProfiles_Machines_MachineId",
                        column: x => x.MachineId,
                        principalTable: "Machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OperatorSetupProfiles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SetupDispatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DispatchNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    MachineId = table.Column<int>(type: "INTEGER", nullable: false),
                    MachineProgramId = table.Column<int>(type: "INTEGER", nullable: true),
                    StageExecutionId = table.Column<int>(type: "INTEGER", nullable: true),
                    JobId = table.Column<int>(type: "INTEGER", nullable: true),
                    PartId = table.Column<int>(type: "INTEGER", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    DispatchType = table.Column<int>(type: "INTEGER", nullable: false),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    PriorityReason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ScoreBreakdownJson = table.Column<string>(type: "TEXT", nullable: true),
                    AssignedOperatorId = table.Column<int>(type: "INTEGER", nullable: true),
                    RequestedByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    EstimatedSetupMinutes = table.Column<double>(type: "REAL", nullable: true),
                    ActualSetupMinutes = table.Column<double>(type: "REAL", nullable: true),
                    EstimatedChangeoverMinutes = table.Column<double>(type: "REAL", nullable: true),
                    ActualChangeoverMinutes = table.Column<double>(type: "REAL", nullable: true),
                    ToolingRequired = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    FixtureRequired = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    WorkInstructionId = table.Column<int>(type: "INTEGER", nullable: true),
                    ScheduledStartAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    QueuedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    VerifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ChangeoverFromProgramId = table.Column<int>(type: "INTEGER", nullable: true),
                    ChangeoverToProgramId = table.Column<int>(type: "INTEGER", nullable: true),
                    PredecessorDispatchId = table.Column<int>(type: "INTEGER", nullable: true),
                    MaintenanceWorkOrderId = table.Column<int>(type: "INTEGER", nullable: true),
                    IsAutoGenerated = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    LastModifiedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SetupDispatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SetupDispatches_Jobs_JobId",
                        column: x => x.JobId,
                        principalTable: "Jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SetupDispatches_MachinePrograms_MachineProgramId",
                        column: x => x.MachineProgramId,
                        principalTable: "MachinePrograms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SetupDispatches_Machines_MachineId",
                        column: x => x.MachineId,
                        principalTable: "Machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SetupDispatches_Parts_PartId",
                        column: x => x.PartId,
                        principalTable: "Parts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SetupDispatches_SetupDispatches_PredecessorDispatchId",
                        column: x => x.PredecessorDispatchId,
                        principalTable: "SetupDispatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SetupDispatches_StageExecutions_StageExecutionId",
                        column: x => x.StageExecutionId,
                        principalTable: "StageExecutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SetupDispatches_Users_AssignedOperatorId",
                        column: x => x.AssignedOperatorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SetupDispatches_Users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SetupDispatches_WorkInstructions_WorkInstructionId",
                        column: x => x.WorkInstructionId,
                        principalTable: "WorkInstructions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SetupHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SetupDispatchId = table.Column<int>(type: "INTEGER", nullable: false),
                    MachineId = table.Column<int>(type: "INTEGER", nullable: false),
                    MachineProgramId = table.Column<int>(type: "INTEGER", nullable: true),
                    PartId = table.Column<int>(type: "INTEGER", nullable: true),
                    OperatorUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    SetupDurationMinutes = table.Column<double>(type: "REAL", nullable: false),
                    ChangeoverDurationMinutes = table.Column<double>(type: "REAL", nullable: true),
                    WasChangeover = table.Column<bool>(type: "INTEGER", nullable: false),
                    PreviousProgramId = table.Column<int>(type: "INTEGER", nullable: true),
                    ToolingUsedJson = table.Column<string>(type: "TEXT", nullable: true),
                    QualityResult = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ShiftId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SetupHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SetupHistories_MachinePrograms_MachineProgramId",
                        column: x => x.MachineProgramId,
                        principalTable: "MachinePrograms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SetupHistories_Machines_MachineId",
                        column: x => x.MachineId,
                        principalTable: "Machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SetupHistories_OperatingShifts_ShiftId",
                        column: x => x.ShiftId,
                        principalTable: "OperatingShifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SetupHistories_SetupDispatches_SetupDispatchId",
                        column: x => x.SetupDispatchId,
                        principalTable: "SetupDispatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SetupHistories_Users_OperatorUserId",
                        column: x => x.OperatorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StageExecutions_SetupDispatchId",
                table: "StageExecutions",
                column: "SetupDispatchId");

            migrationBuilder.CreateIndex(
                name: "IX_Machines_CurrentProgramId",
                table: "Machines",
                column: "CurrentProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_DispatchConfigurations_MachineId",
                table: "DispatchConfigurations",
                column: "MachineId");

            migrationBuilder.CreateIndex(
                name: "IX_DispatchConfigurations_ProductionStageId",
                table: "DispatchConfigurations",
                column: "ProductionStageId");

            migrationBuilder.CreateIndex(
                name: "IX_OperatorSetupProfiles_MachineId",
                table: "OperatorSetupProfiles",
                column: "MachineId");

            migrationBuilder.CreateIndex(
                name: "IX_OperatorSetupProfiles_MachineProgramId",
                table: "OperatorSetupProfiles",
                column: "MachineProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_OperatorSetupProfiles_UserId_MachineId_MachineProgramId",
                table: "OperatorSetupProfiles",
                columns: new[] { "UserId", "MachineId", "MachineProgramId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SetupDispatches_AssignedOperatorId",
                table: "SetupDispatches",
                column: "AssignedOperatorId");

            migrationBuilder.CreateIndex(
                name: "IX_SetupDispatches_DispatchNumber",
                table: "SetupDispatches",
                column: "DispatchNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SetupDispatches_JobId",
                table: "SetupDispatches",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_SetupDispatches_MachineId_Status",
                table: "SetupDispatches",
                columns: new[] { "MachineId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SetupDispatches_MachineProgramId",
                table: "SetupDispatches",
                column: "MachineProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_SetupDispatches_PartId",
                table: "SetupDispatches",
                column: "PartId");

            migrationBuilder.CreateIndex(
                name: "IX_SetupDispatches_PredecessorDispatchId",
                table: "SetupDispatches",
                column: "PredecessorDispatchId");

            migrationBuilder.CreateIndex(
                name: "IX_SetupDispatches_RequestedByUserId",
                table: "SetupDispatches",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SetupDispatches_StageExecutionId",
                table: "SetupDispatches",
                column: "StageExecutionId");

            migrationBuilder.CreateIndex(
                name: "IX_SetupDispatches_Status",
                table: "SetupDispatches",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SetupDispatches_WorkInstructionId",
                table: "SetupDispatches",
                column: "WorkInstructionId");

            migrationBuilder.CreateIndex(
                name: "IX_SetupHistories_MachineId_CompletedAt",
                table: "SetupHistories",
                columns: new[] { "MachineId", "CompletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SetupHistories_MachineProgramId",
                table: "SetupHistories",
                column: "MachineProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_SetupHistories_OperatorUserId_MachineId",
                table: "SetupHistories",
                columns: new[] { "OperatorUserId", "MachineId" });

            migrationBuilder.CreateIndex(
                name: "IX_SetupHistories_SetupDispatchId",
                table: "SetupHistories",
                column: "SetupDispatchId");

            migrationBuilder.CreateIndex(
                name: "IX_SetupHistories_ShiftId",
                table: "SetupHistories",
                column: "ShiftId");

            migrationBuilder.AddForeignKey(
                name: "FK_Machines_MachinePrograms_CurrentProgramId",
                table: "Machines",
                column: "CurrentProgramId",
                principalTable: "MachinePrograms",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_StageExecutions_SetupDispatches_SetupDispatchId",
                table: "StageExecutions",
                column: "SetupDispatchId",
                principalTable: "SetupDispatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Machines_MachinePrograms_CurrentProgramId",
                table: "Machines");

            migrationBuilder.DropForeignKey(
                name: "FK_StageExecutions_SetupDispatches_SetupDispatchId",
                table: "StageExecutions");

            migrationBuilder.DropTable(
                name: "DispatchConfigurations");

            migrationBuilder.DropTable(
                name: "OperatorSetupProfiles");

            migrationBuilder.DropTable(
                name: "SetupHistories");

            migrationBuilder.DropTable(
                name: "SetupDispatches");

            migrationBuilder.DropIndex(
                name: "IX_StageExecutions_SetupDispatchId",
                table: "StageExecutions");

            migrationBuilder.DropIndex(
                name: "IX_Machines_CurrentProgramId",
                table: "Machines");

            migrationBuilder.DropColumn(
                name: "SetupDispatchId",
                table: "StageExecutions");

            migrationBuilder.DropColumn(
                name: "CurrentProgramId",
                table: "Machines");

            migrationBuilder.DropColumn(
                name: "LastSetupChangeAt",
                table: "Machines");

            migrationBuilder.DropColumn(
                name: "SetupState",
                table: "Machines");

            migrationBuilder.DropColumn(
                name: "ActualAverageSetupMinutes",
                table: "MachinePrograms");

            migrationBuilder.DropColumn(
                name: "SetupSampleCount",
                table: "MachinePrograms");

            migrationBuilder.DropColumn(
                name: "SetupVarianceMinutes",
                table: "MachinePrograms");
        }
    }
}
