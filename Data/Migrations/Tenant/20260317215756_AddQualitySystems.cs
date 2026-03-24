using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Opcentrix_V3.Data.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddQualitySystems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CorrectiveAction",
                table: "QCInspections",
                newName: "CorrectiveActionText");

            migrationBuilder.AddColumn<int>(
                name: "InspectionPlanId",
                table: "QCInspections",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFair",
                table: "QCInspections",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "NonConformanceReportId",
                table: "QCInspections",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OverallResult",
                table: "QCInspections",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "CorrectiveActions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CapaNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    ProblemStatement = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    RootCauseAnalysis = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    ImmediateAction = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    LongTermAction = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    PreventiveAction = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    OwnerId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    DueDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EffectivenessVerification = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorrectiveActions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InspectionMeasurements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    QcInspectionId = table.Column<int>(type: "INTEGER", nullable: false),
                    CharacteristicName = table.Column<string>(type: "TEXT", nullable: false),
                    DrawingCallout = table.Column<string>(type: "TEXT", nullable: true),
                    NominalValue = table.Column<decimal>(type: "TEXT", nullable: false),
                    TolerancePlus = table.Column<decimal>(type: "TEXT", nullable: false),
                    ToleranceMinus = table.Column<decimal>(type: "TEXT", nullable: false),
                    ActualValue = table.Column<decimal>(type: "TEXT", nullable: false),
                    Deviation = table.Column<decimal>(type: "TEXT", nullable: false),
                    IsInSpec = table.Column<bool>(type: "INTEGER", nullable: false),
                    InstrumentUsed = table.Column<string>(type: "TEXT", nullable: true),
                    GageId = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionMeasurements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InspectionMeasurements_QCInspections_QcInspectionId",
                        column: x => x.QcInspectionId,
                        principalTable: "QCInspections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InspectionPlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PartId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Revision = table.Column<string>(type: "TEXT", nullable: true),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InspectionPlans_Parts_PartId",
                        column: x => x.PartId,
                        principalTable: "Parts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SpcDataPoints",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PartId = table.Column<int>(type: "INTEGER", nullable: false),
                    CharacteristicName = table.Column<string>(type: "TEXT", nullable: false),
                    MeasuredValue = table.Column<decimal>(type: "TEXT", nullable: false),
                    NominalValue = table.Column<decimal>(type: "TEXT", nullable: false),
                    TolerancePlus = table.Column<decimal>(type: "TEXT", nullable: false),
                    ToleranceMinus = table.Column<decimal>(type: "TEXT", nullable: false),
                    QcInspectionId = table.Column<int>(type: "INTEGER", nullable: true),
                    JobId = table.Column<int>(type: "INTEGER", nullable: true),
                    RecordedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpcDataPoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SpcDataPoints_Parts_PartId",
                        column: x => x.PartId,
                        principalTable: "Parts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NonConformanceReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    NcrNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    JobId = table.Column<int>(type: "INTEGER", nullable: true),
                    PartId = table.Column<int>(type: "INTEGER", nullable: true),
                    PartInstanceId = table.Column<int>(type: "INTEGER", nullable: true),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    QuantityAffected = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Severity = table.Column<int>(type: "INTEGER", nullable: false),
                    Disposition = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ReportedByUserId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ReportedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CorrectiveActionId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NonConformanceReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NonConformanceReports_CorrectiveActions_CorrectiveActionId",
                        column: x => x.CorrectiveActionId,
                        principalTable: "CorrectiveActions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_NonConformanceReports_Jobs_JobId",
                        column: x => x.JobId,
                        principalTable: "Jobs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_NonConformanceReports_PartInstances_PartInstanceId",
                        column: x => x.PartInstanceId,
                        principalTable: "PartInstances",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_NonConformanceReports_Parts_PartId",
                        column: x => x.PartId,
                        principalTable: "Parts",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "InspectionPlanCharacteristics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    InspectionPlanId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    DrawingCallout = table.Column<string>(type: "TEXT", nullable: true),
                    NominalValue = table.Column<decimal>(type: "TEXT", nullable: false),
                    TolerancePlus = table.Column<decimal>(type: "TEXT", nullable: false),
                    ToleranceMinus = table.Column<decimal>(type: "TEXT", nullable: false),
                    InstrumentType = table.Column<string>(type: "TEXT", nullable: true),
                    IsKeyCharacteristic = table.Column<bool>(type: "INTEGER", nullable: false),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionPlanCharacteristics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InspectionPlanCharacteristics_InspectionPlans_InspectionPlanId",
                        column: x => x.InspectionPlanId,
                        principalTable: "InspectionPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QCInspections_InspectionPlanId",
                table: "QCInspections",
                column: "InspectionPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_CorrectiveActions_CapaNumber",
                table: "CorrectiveActions",
                column: "CapaNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InspectionMeasurements_QcInspectionId",
                table: "InspectionMeasurements",
                column: "QcInspectionId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionPlanCharacteristics_InspectionPlanId",
                table: "InspectionPlanCharacteristics",
                column: "InspectionPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionPlans_PartId",
                table: "InspectionPlans",
                column: "PartId");

            migrationBuilder.CreateIndex(
                name: "IX_NonConformanceReports_CorrectiveActionId",
                table: "NonConformanceReports",
                column: "CorrectiveActionId");

            migrationBuilder.CreateIndex(
                name: "IX_NonConformanceReports_JobId",
                table: "NonConformanceReports",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_NonConformanceReports_NcrNumber",
                table: "NonConformanceReports",
                column: "NcrNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NonConformanceReports_PartId",
                table: "NonConformanceReports",
                column: "PartId");

            migrationBuilder.CreateIndex(
                name: "IX_NonConformanceReports_PartInstanceId",
                table: "NonConformanceReports",
                column: "PartInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_SpcDataPoints_PartId_CharacteristicName",
                table: "SpcDataPoints",
                columns: new[] { "PartId", "CharacteristicName" });

            migrationBuilder.AddForeignKey(
                name: "FK_QCInspections_InspectionPlans_InspectionPlanId",
                table: "QCInspections",
                column: "InspectionPlanId",
                principalTable: "InspectionPlans",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_QCInspections_InspectionPlans_InspectionPlanId",
                table: "QCInspections");

            migrationBuilder.DropTable(
                name: "InspectionMeasurements");

            migrationBuilder.DropTable(
                name: "InspectionPlanCharacteristics");

            migrationBuilder.DropTable(
                name: "NonConformanceReports");

            migrationBuilder.DropTable(
                name: "SpcDataPoints");

            migrationBuilder.DropTable(
                name: "InspectionPlans");

            migrationBuilder.DropTable(
                name: "CorrectiveActions");

            migrationBuilder.DropIndex(
                name: "IX_QCInspections_InspectionPlanId",
                table: "QCInspections");

            migrationBuilder.DropColumn(
                name: "InspectionPlanId",
                table: "QCInspections");

            migrationBuilder.DropColumn(
                name: "IsFair",
                table: "QCInspections");

            migrationBuilder.DropColumn(
                name: "NonConformanceReportId",
                table: "QCInspections");

            migrationBuilder.DropColumn(
                name: "OverallResult",
                table: "QCInspections");

            migrationBuilder.RenameColumn(
                name: "CorrectiveActionText",
                table: "QCInspections",
                newName: "CorrectiveAction");
        }
    }
}
