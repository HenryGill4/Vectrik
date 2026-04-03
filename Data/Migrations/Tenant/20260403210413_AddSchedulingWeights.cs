using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vectrik.Data.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddSchedulingWeights : Migration
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
                name: "SchedulingWeights",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BaseScore = table.Column<int>(type: "INTEGER", nullable: false),
                    ChangeoverAlignmentBonus = table.Column<int>(type: "INTEGER", nullable: false),
                    DowntimePenaltyPerHour = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxDowntimePenalty = table.Column<int>(type: "INTEGER", nullable: false),
                    EarlinessBonus4h = table.Column<int>(type: "INTEGER", nullable: false),
                    EarlinessBonus24h = table.Column<int>(type: "INTEGER", nullable: false),
                    OverproductionPenaltyMax = table.Column<int>(type: "INTEGER", nullable: false),
                    WeekendOptimizationBonus = table.Column<int>(type: "INTEGER", nullable: false),
                    ShiftAlignedBonus = table.Column<int>(type: "INTEGER", nullable: false),
                    StackChangeoverBonus = table.Column<int>(type: "INTEGER", nullable: false),
                    StackDemandFitBonus = table.Column<int>(type: "INTEGER", nullable: false),
                    StackEfficiencyMultiplier = table.Column<decimal>(type: "TEXT", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulingWeights", x => x.Id);
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
                name: "SchedulingWeights");

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
