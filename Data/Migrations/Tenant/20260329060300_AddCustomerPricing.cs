using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vectrik.Data.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddCustomerPricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Customer table ───────────────────────────────────
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

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Name",
                table: "Customers",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Code",
                table: "Customers",
                column: "Code",
                unique: true,
                filter: "\"Code\" IS NOT NULL");

            // ── CustomerPricingRule table ─────────────────────────
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

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPricingRules_CustomerId_PartId",
                table: "CustomerPricingRules",
                columns: new[] { "CustomerId", "PartId" });

            // ── PricingContract table ────────────────────────────
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
                name: "IX_PricingContracts_ContractNumber",
                table: "PricingContracts",
                column: "ContractNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PricingContracts_CustomerId",
                table: "PricingContracts",
                column: "CustomerId");

            // ── Quote table additions ────────────────────────────
            migrationBuilder.AddColumn<int>(
                name: "CustomerId",
                table: "Quotes",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PricingContractId",
                table: "Quotes",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CustomerDiscountPct",
                table: "Quotes",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_CustomerId",
                table: "Quotes",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_PricingContractId",
                table: "Quotes",
                column: "PricingContractId");

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

            // ── QuoteLine addition ───────────────────────────────
            migrationBuilder.AddColumn<decimal>(
                name: "StandardPricePerPart",
                table: "QuoteLines",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_Quotes_PricingContracts_PricingContractId", table: "Quotes");
            migrationBuilder.DropForeignKey(name: "FK_Quotes_Customers_CustomerId", table: "Quotes");
            migrationBuilder.DropIndex(name: "IX_Quotes_PricingContractId", table: "Quotes");
            migrationBuilder.DropIndex(name: "IX_Quotes_CustomerId", table: "Quotes");
            migrationBuilder.DropColumn(name: "CustomerId", table: "Quotes");
            migrationBuilder.DropColumn(name: "PricingContractId", table: "Quotes");
            migrationBuilder.DropColumn(name: "CustomerDiscountPct", table: "Quotes");
            migrationBuilder.DropColumn(name: "StandardPricePerPart", table: "QuoteLines");
            migrationBuilder.DropTable(name: "CustomerPricingRules");
            migrationBuilder.DropTable(name: "PricingContracts");
            migrationBuilder.DropTable(name: "Customers");
        }
    }
}
