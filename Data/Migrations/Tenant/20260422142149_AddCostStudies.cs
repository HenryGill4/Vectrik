using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vectrik.Data.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddCostStudies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CostStudies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    StudyNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    CustomerName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ProjectName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    TargetMarginPercent = table.Column<double>(type: "REAL", nullable: false),
                    ContingencyPercent = table.Column<double>(type: "REAL", nullable: false),
                    AdminOverheadPercent = table.Column<double>(type: "REAL", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostStudies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CostStudyParts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CostStudyId = table.Column<int>(type: "INTEGER", nullable: false),
                    PartId = table.Column<int>(type: "INTEGER", nullable: true),
                    PartNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    OrderQuantity = table.Column<int>(type: "INTEGER", nullable: false),
                    MaterialId = table.Column<int>(type: "INTEGER", nullable: true),
                    MaterialName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    MaterialCostPerKg = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    WeightPerPartKg = table.Column<decimal>(type: "decimal(10,4)", nullable: false),
                    MaterialScrapPercent = table.Column<double>(type: "REAL", nullable: false),
                    IsAdditive = table.Column<bool>(type: "INTEGER", nullable: false),
                    PartsPerPlate = table.Column<int>(type: "INTEGER", nullable: false),
                    PlateBuildHours = table.Column<double>(type: "REAL", nullable: false),
                    StackLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    MachineHourlyRate = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    ConsumablesPerPlate = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostStudyParts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CostStudyParts_CostStudies_CostStudyId",
                        column: x => x.CostStudyId,
                        principalTable: "CostStudies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CostStudyParts_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CostStudyParts_Parts_PartId",
                        column: x => x.PartId,
                        principalTable: "Parts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CostStudyStages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CostStudyPartId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductionStageId = table.Column<int>(type: "INTEGER", nullable: true),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    StageName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    SetupMinutes = table.Column<double>(type: "REAL", nullable: false),
                    MinutesPerPart = table.Column<double>(type: "REAL", nullable: false),
                    BatchMinutes = table.Column<double>(type: "REAL", nullable: false),
                    BatchSize = table.Column<int>(type: "INTEGER", nullable: false),
                    HourlyRate = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    OperatorCount = table.Column<int>(type: "INTEGER", nullable: false),
                    OverheadPercent = table.Column<double>(type: "REAL", nullable: false),
                    MaterialCostPerPart = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    ConsumablesPerPart = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    ToolingCostPerRun = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    IsExternal = table.Column<bool>(type: "INTEGER", nullable: false),
                    ExternalVendorCostPerPart = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    ExternalShippingCost = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    ExternalMarkupPercent = table.Column<double>(type: "REAL", nullable: false),
                    YieldPercent = table.Column<double>(type: "REAL", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostStudyStages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CostStudyStages_CostStudyParts_CostStudyPartId",
                        column: x => x.CostStudyPartId,
                        principalTable: "CostStudyParts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CostStudyStages_ProductionStages_ProductionStageId",
                        column: x => x.ProductionStageId,
                        principalTable: "ProductionStages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CostStudies_Status",
                table: "CostStudies",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CostStudies_StudyNumber",
                table: "CostStudies",
                column: "StudyNumber");

            migrationBuilder.CreateIndex(
                name: "IX_CostStudyParts_CostStudyId",
                table: "CostStudyParts",
                column: "CostStudyId");

            migrationBuilder.CreateIndex(
                name: "IX_CostStudyParts_MaterialId",
                table: "CostStudyParts",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_CostStudyParts_PartId",
                table: "CostStudyParts",
                column: "PartId");

            migrationBuilder.CreateIndex(
                name: "IX_CostStudyStages_CostStudyPartId",
                table: "CostStudyStages",
                column: "CostStudyPartId");

            migrationBuilder.CreateIndex(
                name: "IX_CostStudyStages_ProductionStageId",
                table: "CostStudyStages",
                column: "ProductionStageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CostStudyStages");

            migrationBuilder.DropTable(
                name: "CostStudyParts");

            migrationBuilder.DropTable(
                name: "CostStudies");
        }
    }
}
