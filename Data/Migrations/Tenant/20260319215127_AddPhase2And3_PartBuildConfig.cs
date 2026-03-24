using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Opcentrix_V3.Data.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddPhase2And3_PartBuildConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowStacking",
                table: "Parts");

            migrationBuilder.DropColumn(
                name: "DepowderingDurationHours",
                table: "Parts");

            migrationBuilder.DropColumn(
                name: "DepowderingPartsPerBatch",
                table: "Parts");

            migrationBuilder.DropColumn(
                name: "DoubleStackDurationHours",
                table: "Parts");

            migrationBuilder.DropColumn(
                name: "EnableDoubleStack",
                table: "Parts");

            migrationBuilder.DropColumn(
                name: "EnableTripleStack",
                table: "Parts");

            migrationBuilder.DropColumn(
                name: "HeatTreatmentDurationHours",
                table: "Parts");

            migrationBuilder.DropColumn(
                name: "HeatTreatmentPartsPerBatch",
                table: "Parts");

            migrationBuilder.DropColumn(
                name: "MaxStackCount",
                table: "Parts");

            migrationBuilder.DropColumn(
                name: "PartsPerBuildDouble",
                table: "Parts");

            migrationBuilder.DropColumn(
                name: "PartsPerBuildSingle",
                table: "Parts");

            migrationBuilder.DropColumn(
                name: "PartsPerBuildTriple",
                table: "Parts");

            migrationBuilder.DropColumn(
                name: "SingleStackDurationHours",
                table: "Parts");

            migrationBuilder.DropColumn(
                name: "SlsBuildDurationHours",
                table: "Parts");

            migrationBuilder.DropColumn(
                name: "SlsPartsPerBuild",
                table: "Parts");

            migrationBuilder.DropColumn(
                name: "StageEstimateSingle",
                table: "Parts");

            migrationBuilder.DropColumn(
                name: "TripleStackDurationHours",
                table: "Parts");

            migrationBuilder.DropColumn(
                name: "WireEdmDurationHours",
                table: "Parts");

            migrationBuilder.DropColumn(
                name: "WireEdmPartsPerSession",
                table: "Parts");

            migrationBuilder.AddColumn<string>(
                name: "BatchGroupId",
                table: "StageExecutions",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BatchPartCount",
                table: "StageExecutions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AutoChangeoverEnabled",
                table: "Machines",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "BuildPlateCapacity",
                table: "Machines",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "ChangeoverMinutes",
                table: "Machines",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "LaserCount",
                table: "Machines",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsLocked",
                table: "BuildPackages",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSlicerDataEntered",
                table: "BuildPackages",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "PlateReleasedAt",
                table: "BuildPackages",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PredecessorBuildPackageId",
                table: "BuildPackages",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PrintCompletedAt",
                table: "BuildPackages",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PrintStartedAt",
                table: "BuildPackages",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SlicerNotes",
                table: "BuildPackageParts",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StackLevel",
                table: "BuildPackageParts",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "PartAdditiveBuildConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PartId = table.Column<int>(type: "INTEGER", nullable: false),
                    AllowStacking = table.Column<bool>(type: "INTEGER", nullable: false),
                    MaxStackCount = table.Column<int>(type: "INTEGER", nullable: false),
                    SingleStackDurationHours = table.Column<double>(type: "REAL", nullable: true),
                    DoubleStackDurationHours = table.Column<double>(type: "REAL", nullable: true),
                    TripleStackDurationHours = table.Column<double>(type: "REAL", nullable: true),
                    PlannedPartsPerBuildSingle = table.Column<int>(type: "INTEGER", nullable: false),
                    PlannedPartsPerBuildDouble = table.Column<int>(type: "INTEGER", nullable: true),
                    PlannedPartsPerBuildTriple = table.Column<int>(type: "INTEGER", nullable: true),
                    EnableDoubleStack = table.Column<bool>(type: "INTEGER", nullable: false),
                    EnableTripleStack = table.Column<bool>(type: "INTEGER", nullable: false),
                    DepowderingDurationHours = table.Column<double>(type: "REAL", nullable: true),
                    DepowderingPartsPerBatch = table.Column<int>(type: "INTEGER", nullable: true),
                    HeatTreatmentDurationHours = table.Column<double>(type: "REAL", nullable: true),
                    HeatTreatmentPartsPerBatch = table.Column<int>(type: "INTEGER", nullable: true),
                    WireEdmDurationHours = table.Column<double>(type: "REAL", nullable: true),
                    WireEdmPartsPerSession = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartAdditiveBuildConfigs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartAdditiveBuildConfigs_Parts_PartId",
                        column: x => x.PartId,
                        principalTable: "Parts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BuildPackages_PredecessorBuildPackageId",
                table: "BuildPackages",
                column: "PredecessorBuildPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_PartAdditiveBuildConfigs_PartId",
                table: "PartAdditiveBuildConfigs",
                column: "PartId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_BuildPackages_BuildPackages_PredecessorBuildPackageId",
                table: "BuildPackages",
                column: "PredecessorBuildPackageId",
                principalTable: "BuildPackages",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BuildPackages_BuildPackages_PredecessorBuildPackageId",
                table: "BuildPackages");

            migrationBuilder.DropTable(
                name: "PartAdditiveBuildConfigs");

            migrationBuilder.DropIndex(
                name: "IX_BuildPackages_PredecessorBuildPackageId",
                table: "BuildPackages");

            migrationBuilder.DropColumn(
                name: "BatchGroupId",
                table: "StageExecutions");

            migrationBuilder.DropColumn(
                name: "BatchPartCount",
                table: "StageExecutions");

            migrationBuilder.DropColumn(
                name: "AutoChangeoverEnabled",
                table: "Machines");

            migrationBuilder.DropColumn(
                name: "BuildPlateCapacity",
                table: "Machines");

            migrationBuilder.DropColumn(
                name: "ChangeoverMinutes",
                table: "Machines");

            migrationBuilder.DropColumn(
                name: "LaserCount",
                table: "Machines");

            migrationBuilder.DropColumn(
                name: "IsLocked",
                table: "BuildPackages");

            migrationBuilder.DropColumn(
                name: "IsSlicerDataEntered",
                table: "BuildPackages");

            migrationBuilder.DropColumn(
                name: "PlateReleasedAt",
                table: "BuildPackages");

            migrationBuilder.DropColumn(
                name: "PredecessorBuildPackageId",
                table: "BuildPackages");

            migrationBuilder.DropColumn(
                name: "PrintCompletedAt",
                table: "BuildPackages");

            migrationBuilder.DropColumn(
                name: "PrintStartedAt",
                table: "BuildPackages");

            migrationBuilder.DropColumn(
                name: "SlicerNotes",
                table: "BuildPackageParts");

            migrationBuilder.DropColumn(
                name: "StackLevel",
                table: "BuildPackageParts");

            migrationBuilder.AddColumn<bool>(
                name: "AllowStacking",
                table: "Parts",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<double>(
                name: "DepowderingDurationHours",
                table: "Parts",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DepowderingPartsPerBatch",
                table: "Parts",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "DoubleStackDurationHours",
                table: "Parts",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EnableDoubleStack",
                table: "Parts",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnableTripleStack",
                table: "Parts",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<double>(
                name: "HeatTreatmentDurationHours",
                table: "Parts",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HeatTreatmentPartsPerBatch",
                table: "Parts",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxStackCount",
                table: "Parts",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PartsPerBuildDouble",
                table: "Parts",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PartsPerBuildSingle",
                table: "Parts",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PartsPerBuildTriple",
                table: "Parts",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "SingleStackDurationHours",
                table: "Parts",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "SlsBuildDurationHours",
                table: "Parts",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SlsPartsPerBuild",
                table: "Parts",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "StageEstimateSingle",
                table: "Parts",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "TripleStackDurationHours",
                table: "Parts",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "WireEdmDurationHours",
                table: "Parts",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WireEdmPartsPerSession",
                table: "Parts",
                type: "INTEGER",
                nullable: true);
        }
    }
}
