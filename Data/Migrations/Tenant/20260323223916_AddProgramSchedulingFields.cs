using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Opcentrix_V3.Data.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddProgramSchedulingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsLocked",
                table: "MachinePrograms",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "PlateReleasedAt",
                table: "MachinePrograms",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PredecessorProgramId",
                table: "MachinePrograms",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PrintCompletedAt",
                table: "MachinePrograms",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PrintStartedAt",
                table: "MachinePrograms",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScheduleStatus",
                table: "MachinePrograms",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ScheduledDate",
                table: "MachinePrograms",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScheduledJobId",
                table: "MachinePrograms",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceProgramId",
                table: "MachinePrograms",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProgramRevisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MachineProgramId = table.Column<int>(type: "INTEGER", nullable: false),
                    RevisionNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    RevisionDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ChangedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ChangeNotes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    PartsSnapshotJson = table.Column<string>(type: "TEXT", nullable: false),
                    ParametersSnapshotJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgramRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProgramRevisions_MachinePrograms_MachineProgramId",
                        column: x => x.MachineProgramId,
                        principalTable: "MachinePrograms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MachinePrograms_PredecessorProgramId",
                table: "MachinePrograms",
                column: "PredecessorProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_MachinePrograms_ScheduledDate",
                table: "MachinePrograms",
                column: "ScheduledDate");

            migrationBuilder.CreateIndex(
                name: "IX_MachinePrograms_ScheduledJobId",
                table: "MachinePrograms",
                column: "ScheduledJobId");

            migrationBuilder.CreateIndex(
                name: "IX_MachinePrograms_ScheduleStatus",
                table: "MachinePrograms",
                column: "ScheduleStatus");

            migrationBuilder.CreateIndex(
                name: "IX_MachinePrograms_SourceProgramId",
                table: "MachinePrograms",
                column: "SourceProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramRevisions_MachineProgramId_RevisionNumber",
                table: "ProgramRevisions",
                columns: new[] { "MachineProgramId", "RevisionNumber" });

            migrationBuilder.AddForeignKey(
                name: "FK_MachinePrograms_Jobs_ScheduledJobId",
                table: "MachinePrograms",
                column: "ScheduledJobId",
                principalTable: "Jobs",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_MachinePrograms_MachinePrograms_PredecessorProgramId",
                table: "MachinePrograms",
                column: "PredecessorProgramId",
                principalTable: "MachinePrograms",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_MachinePrograms_MachinePrograms_SourceProgramId",
                table: "MachinePrograms",
                column: "SourceProgramId",
                principalTable: "MachinePrograms",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MachinePrograms_Jobs_ScheduledJobId",
                table: "MachinePrograms");

            migrationBuilder.DropForeignKey(
                name: "FK_MachinePrograms_MachinePrograms_PredecessorProgramId",
                table: "MachinePrograms");

            migrationBuilder.DropForeignKey(
                name: "FK_MachinePrograms_MachinePrograms_SourceProgramId",
                table: "MachinePrograms");

            migrationBuilder.DropTable(
                name: "ProgramRevisions");

            migrationBuilder.DropIndex(
                name: "IX_MachinePrograms_PredecessorProgramId",
                table: "MachinePrograms");

            migrationBuilder.DropIndex(
                name: "IX_MachinePrograms_ScheduledDate",
                table: "MachinePrograms");

            migrationBuilder.DropIndex(
                name: "IX_MachinePrograms_ScheduledJobId",
                table: "MachinePrograms");

            migrationBuilder.DropIndex(
                name: "IX_MachinePrograms_ScheduleStatus",
                table: "MachinePrograms");

            migrationBuilder.DropIndex(
                name: "IX_MachinePrograms_SourceProgramId",
                table: "MachinePrograms");

            migrationBuilder.DropColumn(
                name: "IsLocked",
                table: "MachinePrograms");

            migrationBuilder.DropColumn(
                name: "PlateReleasedAt",
                table: "MachinePrograms");

            migrationBuilder.DropColumn(
                name: "PredecessorProgramId",
                table: "MachinePrograms");

            migrationBuilder.DropColumn(
                name: "PrintCompletedAt",
                table: "MachinePrograms");

            migrationBuilder.DropColumn(
                name: "PrintStartedAt",
                table: "MachinePrograms");

            migrationBuilder.DropColumn(
                name: "ScheduleStatus",
                table: "MachinePrograms");

            migrationBuilder.DropColumn(
                name: "ScheduledDate",
                table: "MachinePrograms");

            migrationBuilder.DropColumn(
                name: "ScheduledJobId",
                table: "MachinePrograms");

            migrationBuilder.DropColumn(
                name: "SourceProgramId",
                table: "MachinePrograms");
        }
    }
}
