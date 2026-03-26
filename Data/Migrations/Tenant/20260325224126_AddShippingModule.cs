using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Opcentrix_V3.Data.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddShippingModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Shipments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ShipmentNumber = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    WorkOrderId = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CarrierName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    TrackingNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    PackageCount = table.Column<int>(type: "INTEGER", nullable: false),
                    PackingListJson = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    ShipperNotes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    ShippedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ShippedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shipments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Shipments_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ShipmentLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ShipmentId = table.Column<int>(type: "INTEGER", nullable: false),
                    WorkOrderLineId = table.Column<int>(type: "INTEGER", nullable: false),
                    QuantityShipped = table.Column<decimal>(type: "decimal(18,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShipmentLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShipmentLines_Shipments_ShipmentId",
                        column: x => x.ShipmentId,
                        principalTable: "Shipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ShipmentLines_WorkOrderLines_WorkOrderLineId",
                        column: x => x.WorkOrderLineId,
                        principalTable: "WorkOrderLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentLines_ShipmentId",
                table: "ShipmentLines",
                column: "ShipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentLines_WorkOrderLineId",
                table: "ShipmentLines",
                column: "WorkOrderLineId");

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_WorkOrderId",
                table: "Shipments",
                column: "WorkOrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShipmentLines");

            migrationBuilder.DropTable(
                name: "Shipments");
        }
    }
}
