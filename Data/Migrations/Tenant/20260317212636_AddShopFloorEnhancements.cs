using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vectrik.Data.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddShopFloorEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ActualEndAt",
                table: "StageExecutions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ActualStartAt",
                table: "StageExecutions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssignedOperatorId",
                table: "StageExecutions",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompletionNotes",
                table: "StageExecutions",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FailureReason",
                table: "StageExecutions",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsUnmanned",
                table: "StageExecutions",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MachineId",
                table: "StageExecutions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RunHoursActual",
                table: "StageExecutions",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "ScheduledEndAt",
                table: "StageExecutions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ScheduledStartAt",
                table: "StageExecutions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SetupHoursActual",
                table: "StageExecutions",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "StageExecutions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Category",
                table: "DelayLogs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Resolution",
                table: "DelayLogs",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResolvedAt",
                table: "DelayLogs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAt",
                table: "DelayLogs",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateIndex(
                name: "IX_StageExecutions_MachineId",
                table: "StageExecutions",
                column: "MachineId");

            migrationBuilder.AddForeignKey(
                name: "FK_StageExecutions_Machines_MachineId",
                table: "StageExecutions",
                column: "MachineId",
                principalTable: "Machines",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StageExecutions_Machines_MachineId",
                table: "StageExecutions");

            migrationBuilder.DropIndex(
                name: "IX_StageExecutions_MachineId",
                table: "StageExecutions");

            migrationBuilder.DropColumn(
                name: "ActualEndAt",
                table: "StageExecutions");

            migrationBuilder.DropColumn(
                name: "ActualStartAt",
                table: "StageExecutions");

            migrationBuilder.DropColumn(
                name: "AssignedOperatorId",
                table: "StageExecutions");

            migrationBuilder.DropColumn(
                name: "CompletionNotes",
                table: "StageExecutions");

            migrationBuilder.DropColumn(
                name: "FailureReason",
                table: "StageExecutions");

            migrationBuilder.DropColumn(
                name: "IsUnmanned",
                table: "StageExecutions");

            migrationBuilder.DropColumn(
                name: "MachineId",
                table: "StageExecutions");

            migrationBuilder.DropColumn(
                name: "RunHoursActual",
                table: "StageExecutions");

            migrationBuilder.DropColumn(
                name: "ScheduledEndAt",
                table: "StageExecutions");

            migrationBuilder.DropColumn(
                name: "ScheduledStartAt",
                table: "StageExecutions");

            migrationBuilder.DropColumn(
                name: "SetupHoursActual",
                table: "StageExecutions");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "StageExecutions");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "DelayLogs");

            migrationBuilder.DropColumn(
                name: "Resolution",
                table: "DelayLogs");

            migrationBuilder.DropColumn(
                name: "ResolvedAt",
                table: "DelayLogs");

            migrationBuilder.DropColumn(
                name: "StartedAt",
                table: "DelayLogs");
        }
    }
}
