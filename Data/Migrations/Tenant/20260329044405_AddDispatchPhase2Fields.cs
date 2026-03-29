using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vectrik.Data.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddDispatchPhase2Fields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DemandSummaryJson",
                table: "SetupDispatches",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InspectionChecklistJson",
                table: "SetupDispatches",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrePrintChecklistJson",
                table: "SetupDispatches",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TargetRoleId",
                table: "SetupDispatches",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BatchGroupingWindowHours",
                table: "DispatchConfigurations",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresSchedulerApproval",
                table: "DispatchConfigurations",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_SetupDispatches_TargetRoleId",
                table: "SetupDispatches",
                column: "TargetRoleId");

            migrationBuilder.AddForeignKey(
                name: "FK_SetupDispatches_OperatorRoles_TargetRoleId",
                table: "SetupDispatches",
                column: "TargetRoleId",
                principalTable: "OperatorRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SetupDispatches_OperatorRoles_TargetRoleId",
                table: "SetupDispatches");

            migrationBuilder.DropIndex(
                name: "IX_SetupDispatches_TargetRoleId",
                table: "SetupDispatches");

            migrationBuilder.DropColumn(
                name: "DemandSummaryJson",
                table: "SetupDispatches");

            migrationBuilder.DropColumn(
                name: "InspectionChecklistJson",
                table: "SetupDispatches");

            migrationBuilder.DropColumn(
                name: "PrePrintChecklistJson",
                table: "SetupDispatches");

            migrationBuilder.DropColumn(
                name: "TargetRoleId",
                table: "SetupDispatches");

            migrationBuilder.DropColumn(
                name: "BatchGroupingWindowHours",
                table: "DispatchConfigurations");

            migrationBuilder.DropColumn(
                name: "RequiresSchedulerApproval",
                table: "DispatchConfigurations");
        }
    }
}
