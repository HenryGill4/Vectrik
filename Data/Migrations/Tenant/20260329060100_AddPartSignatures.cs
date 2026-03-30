using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vectrik.Data.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddPartSignatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PartSignatures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PartId = table.Column<int>(type: "INTEGER", nullable: false),
                    WeightKg = table.Column<double>(type: "REAL", nullable: false),
                    MaterialCategory = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    MaterialName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    MaterialCostPerKg = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    StageCount = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalEstimatedHours = table.Column<double>(type: "REAL", nullable: false),
                    TotalSetupMinutes = table.Column<double>(type: "REAL", nullable: false),
                    ManufacturingApproachId = table.Column<int>(type: "INTEGER", nullable: false),
                    IsAdditive = table.Column<bool>(type: "INTEGER", nullable: false),
                    BomItemCount = table.Column<int>(type: "INTEGER", nullable: false),
                    HasStacking = table.Column<bool>(type: "INTEGER", nullable: false),
                    MaxStackLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    PlannedPartsPerBuild = table.Column<int>(type: "INTEGER", nullable: false),
                    ComplexityScore = table.Column<double>(type: "REAL", nullable: false),
                    ActualCostPerPart = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    ActualHoursPerPart = table.Column<double>(type: "REAL", nullable: false),
                    CompletedJobCount = table.Column<int>(type: "INTEGER", nullable: false),
                    AverageJobQuantity = table.Column<int>(type: "INTEGER", nullable: false),
                    ActualMarginPct = table.Column<double>(type: "REAL", nullable: false),
                    CostAccuracyRatio = table.Column<double>(type: "REAL", nullable: false),
                    LastSellPrice = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    EstimatedCostPerPart = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsStale = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartSignatures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartSignatures_Parts_PartId",
                        column: x => x.PartId,
                        principalTable: "Parts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PartSignatures_PartId",
                table: "PartSignatures",
                column: "PartId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PartSignatures");
        }
    }
}
