using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vectrik.Data.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class ExpandCostStudyFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AmortizeNreAcrossOrder",
                table: "CostStudyParts",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "EngineeringNreCost",
                table: "CostStudyParts",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "FirstArticleAndCertCost",
                table: "CostStudyParts",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "FreightCostPerOrder",
                table: "CostStudyParts",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<double>(
                name: "FreightMarkupPercent",
                table: "CostStudyParts",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<decimal>(
                name: "PackagingCostPerOrder",
                table: "CostStudyParts",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PackagingCostPerPart",
                table: "CostStudyParts",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SalesPriceOverridePerPart",
                table: "CostStudyParts",
                type: "decimal(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ToolingNreCost",
                table: "CostStudyParts",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<double>(
                name: "DefaultVendorMarkupPercent",
                table: "CostStudies",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "PaymentTermsDiscountPercent",
                table: "CostStudies",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AmortizeNreAcrossOrder",
                table: "CostStudyParts");

            migrationBuilder.DropColumn(
                name: "EngineeringNreCost",
                table: "CostStudyParts");

            migrationBuilder.DropColumn(
                name: "FirstArticleAndCertCost",
                table: "CostStudyParts");

            migrationBuilder.DropColumn(
                name: "FreightCostPerOrder",
                table: "CostStudyParts");

            migrationBuilder.DropColumn(
                name: "FreightMarkupPercent",
                table: "CostStudyParts");

            migrationBuilder.DropColumn(
                name: "PackagingCostPerOrder",
                table: "CostStudyParts");

            migrationBuilder.DropColumn(
                name: "PackagingCostPerPart",
                table: "CostStudyParts");

            migrationBuilder.DropColumn(
                name: "SalesPriceOverridePerPart",
                table: "CostStudyParts");

            migrationBuilder.DropColumn(
                name: "ToolingNreCost",
                table: "CostStudyParts");

            migrationBuilder.DropColumn(
                name: "DefaultVendorMarkupPercent",
                table: "CostStudies");

            migrationBuilder.DropColumn(
                name: "PaymentTermsDiscountPercent",
                table: "CostStudies");
        }
    }
}
