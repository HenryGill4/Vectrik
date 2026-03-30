using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vectrik.Data.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class SyncAllPendingChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Quotes_WorkOrders_ConvertedWorkOrderId2",
                table: "Quotes");

            migrationBuilder.RenameColumn(
                name: "ConvertedWorkOrderId2",
                table: "Quotes",
                newName: "PricingContractId");

            migrationBuilder.RenameIndex(
                name: "IX_Quotes_ConvertedWorkOrderId2",
                table: "Quotes",
                newName: "IX_Quotes_PricingContractId");

            migrationBuilder.AddColumn<decimal>(
                name: "CompetitorPrice",
                table: "Quotes",
                type: "decimal(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ConvertedWorkOrderId1",
                table: "Quotes",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CustomerDiscountPct",
                table: "Quotes",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "CustomerId",
                table: "Quotes",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DecisionDays",
                table: "Quotes",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LossNotes",
                table: "Quotes",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LossReason",
                table: "Quotes",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "OverheadCostEach",
                table: "QuoteLines",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SetupCostEach",
                table: "QuoteLines",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "StackLevel",
                table: "QuoteLines",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "StandardPricePerPart",
                table: "QuoteLines",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Company = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Address = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Tier = table.Column<int>(type: "INTEGER", nullable: false),
                    DefaultDiscountPct = table.Column<decimal>(type: "TEXT", nullable: false),
                    DefaultMarginPct = table.Column<decimal>(type: "TEXT", nullable: true),
                    PaymentTermDays = table.Column<int>(type: "INTEGER", nullable: false),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    IsDefenseCustomer = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                });

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

            migrationBuilder.CreateTable(
                name: "CustomerPricingRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CustomerId = table.Column<int>(type: "INTEGER", nullable: false),
                    PartId = table.Column<int>(type: "INTEGER", nullable: false),
                    NegotiatedPricePerUnit = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    DiscountPct = table.Column<decimal>(type: "TEXT", nullable: false),
                    MinQuantity = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxQuantity = table.Column<int>(type: "INTEGER", nullable: true),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpirationDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerPricingRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerPricingRules_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CustomerPricingRules_Parts_PartId",
                        column: x => x.PartId,
                        principalTable: "Parts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PricingContracts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CustomerId = table.Column<int>(type: "INTEGER", nullable: false),
                    ContractNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    StartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    BlanketDiscountPct = table.Column<decimal>(type: "TEXT", nullable: false),
                    MinAnnualCommitment = table.Column<decimal>(type: "decimal(12,2)", nullable: true),
                    ActualVolume = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingContracts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PricingContracts_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_ConvertedWorkOrderId1",
                table: "Quotes",
                column: "ConvertedWorkOrderId1");

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_CustomerId",
                table: "Quotes",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPricingRules_CustomerId_PartId",
                table: "CustomerPricingRules",
                columns: new[] { "CustomerId", "PartId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPricingRules_PartId",
                table: "CustomerPricingRules",
                column: "PartId");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Code",
                table: "Customers",
                column: "Code",
                unique: true,
                filter: "\"Code\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Name",
                table: "Customers",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_PartSignatures_PartId",
                table: "PartSignatures",
                column: "PartId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PricingContracts_ContractNumber",
                table: "PricingContracts",
                column: "ContractNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PricingContracts_CustomerId",
                table: "PricingContracts",
                column: "CustomerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Quotes_Customers_CustomerId",
                table: "Quotes",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Quotes_PricingContracts_PricingContractId",
                table: "Quotes",
                column: "PricingContractId",
                principalTable: "PricingContracts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Quotes_WorkOrders_ConvertedWorkOrderId1",
                table: "Quotes",
                column: "ConvertedWorkOrderId1",
                principalTable: "WorkOrders",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Quotes_Customers_CustomerId",
                table: "Quotes");

            migrationBuilder.DropForeignKey(
                name: "FK_Quotes_PricingContracts_PricingContractId",
                table: "Quotes");

            migrationBuilder.DropForeignKey(
                name: "FK_Quotes_WorkOrders_ConvertedWorkOrderId1",
                table: "Quotes");

            migrationBuilder.DropTable(
                name: "CustomerPricingRules");

            migrationBuilder.DropTable(
                name: "PartSignatures");

            migrationBuilder.DropTable(
                name: "PricingContracts");

            migrationBuilder.DropTable(
                name: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Quotes_ConvertedWorkOrderId1",
                table: "Quotes");

            migrationBuilder.DropIndex(
                name: "IX_Quotes_CustomerId",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "CompetitorPrice",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "ConvertedWorkOrderId1",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "CustomerDiscountPct",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "DecisionDays",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "LossNotes",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "LossReason",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "OverheadCostEach",
                table: "QuoteLines");

            migrationBuilder.DropColumn(
                name: "SetupCostEach",
                table: "QuoteLines");

            migrationBuilder.DropColumn(
                name: "StackLevel",
                table: "QuoteLines");

            migrationBuilder.DropColumn(
                name: "StandardPricePerPart",
                table: "QuoteLines");

            migrationBuilder.RenameColumn(
                name: "PricingContractId",
                table: "Quotes",
                newName: "ConvertedWorkOrderId2");

            migrationBuilder.RenameIndex(
                name: "IX_Quotes_PricingContractId",
                table: "Quotes",
                newName: "IX_Quotes_ConvertedWorkOrderId2");

            migrationBuilder.AddForeignKey(
                name: "FK_Quotes_WorkOrders_ConvertedWorkOrderId2",
                table: "Quotes",
                column: "ConvertedWorkOrderId2",
                principalTable: "WorkOrders",
                principalColumn: "Id");
        }
    }
}
