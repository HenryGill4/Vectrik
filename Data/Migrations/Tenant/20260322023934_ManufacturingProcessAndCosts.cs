using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Opcentrix_V3.Data.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class ManufacturingProcessAndCosts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StageCostProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProductionStageId = table.Column<int>(type: "INTEGER", nullable: false),
                    OperatorHourlyRate = table.Column<decimal>(type: "decimal(8,2)", nullable: false),
                    OperatorsRequired = table.Column<int>(type: "INTEGER", nullable: false),
                    SupervisionHourlyRate = table.Column<decimal>(type: "decimal(8,2)", nullable: false),
                    SupervisionAllocationPercent = table.Column<double>(type: "REAL", nullable: false),
                    LaborBurdenPercent = table.Column<double>(type: "REAL", nullable: false),
                    EquipmentHourlyRate = table.Column<decimal>(type: "decimal(8,2)", nullable: false),
                    ToolingCostPerRun = table.Column<decimal>(type: "decimal(8,2)", nullable: false),
                    ConsumablesPerPart = table.Column<decimal>(type: "decimal(8,2)", nullable: false),
                    FacilityHourlyRate = table.Column<decimal>(type: "decimal(8,2)", nullable: false),
                    UtilitiesHourlyRate = table.Column<decimal>(type: "decimal(8,2)", nullable: false),
                    QualityInspectionCostPerPart = table.Column<decimal>(type: "decimal(8,2)", nullable: false),
                    OverheadPercent = table.Column<double>(type: "REAL", nullable: false),
                    ExternalVendorCostPerPart = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    ExternalShippingCost = table.Column<decimal>(type: "decimal(8,2)", nullable: false),
                    ExternalMarkupPercent = table.Column<double>(type: "REAL", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StageCostProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StageCostProfiles_ProductionStages_ProductionStageId",
                        column: x => x.ProductionStageId,
                        principalTable: "ProductionStages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StageCostProfiles_ProductionStageId",
                table: "StageCostProfiles",
                column: "ProductionStageId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StageCostProfiles");
        }
    }
}
