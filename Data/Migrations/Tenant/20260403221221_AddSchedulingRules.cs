using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vectrik.Data.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddSchedulingRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Quotes_WorkOrders_ConvertedWorkOrderId1",
                table: "Quotes");

            migrationBuilder.DropForeignKey(
                name: "FK_ShipmentLines_WorkOrderLines_WorkOrderLineId",
                table: "ShipmentLines");

            migrationBuilder.DropIndex(
                name: "IX_Quotes_ConvertedWorkOrderId1",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "ConvertedWorkOrderId1",
                table: "Quotes");

            migrationBuilder.CreateTable(
                name: "BlackoutPeriods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    StartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsRecurringAnnually = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlackoutPeriods", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MachineSchedulingRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MachineId = table.Column<int>(type: "INTEGER", nullable: false),
                    RuleType = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    MaxConsecutiveBuilds = table.Column<int>(type: "INTEGER", nullable: true),
                    MinBreakHours = table.Column<double>(type: "REAL", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MachineSchedulingRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MachineSchedulingRules_Machines_MachineId",
                        column: x => x.MachineId,
                        principalTable: "Machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MachineBlackoutAssignments",
                columns: table => new
                {
                    MachineId = table.Column<int>(type: "INTEGER", nullable: false),
                    BlackoutPeriodId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MachineBlackoutAssignments", x => new { x.MachineId, x.BlackoutPeriodId });
                    table.ForeignKey(
                        name: "FK_MachineBlackoutAssignments_BlackoutPeriods_BlackoutPeriodId",
                        column: x => x.BlackoutPeriodId,
                        principalTable: "BlackoutPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MachineBlackoutAssignments_Machines_MachineId",
                        column: x => x.MachineId,
                        principalTable: "Machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_ShipmentNumber",
                table: "Shipments",
                column: "ShipmentNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_ConvertedWorkOrderId",
                table: "Quotes",
                column: "ConvertedWorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_MachineBlackoutAssignments_BlackoutPeriodId",
                table: "MachineBlackoutAssignments",
                column: "BlackoutPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_MachineSchedulingRules_MachineId",
                table: "MachineSchedulingRules",
                column: "MachineId");

            migrationBuilder.AddForeignKey(
                name: "FK_Quotes_WorkOrders_ConvertedWorkOrderId",
                table: "Quotes",
                column: "ConvertedWorkOrderId",
                principalTable: "WorkOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ShipmentLines_WorkOrderLines_WorkOrderLineId",
                table: "ShipmentLines",
                column: "WorkOrderLineId",
                principalTable: "WorkOrderLines",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Quotes_WorkOrders_ConvertedWorkOrderId",
                table: "Quotes");

            migrationBuilder.DropForeignKey(
                name: "FK_ShipmentLines_WorkOrderLines_WorkOrderLineId",
                table: "ShipmentLines");

            migrationBuilder.DropTable(
                name: "MachineBlackoutAssignments");

            migrationBuilder.DropTable(
                name: "MachineSchedulingRules");

            migrationBuilder.DropTable(
                name: "BlackoutPeriods");

            migrationBuilder.DropIndex(
                name: "IX_Shipments_ShipmentNumber",
                table: "Shipments");

            migrationBuilder.DropIndex(
                name: "IX_Quotes_ConvertedWorkOrderId",
                table: "Quotes");

            migrationBuilder.AddColumn<int>(
                name: "ConvertedWorkOrderId1",
                table: "Quotes",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_ConvertedWorkOrderId1",
                table: "Quotes",
                column: "ConvertedWorkOrderId1");

            migrationBuilder.AddForeignKey(
                name: "FK_Quotes_WorkOrders_ConvertedWorkOrderId1",
                table: "Quotes",
                column: "ConvertedWorkOrderId1",
                principalTable: "WorkOrders",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ShipmentLines_WorkOrderLines_WorkOrderLineId",
                table: "ShipmentLines",
                column: "WorkOrderLineId",
                principalTable: "WorkOrderLines",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
