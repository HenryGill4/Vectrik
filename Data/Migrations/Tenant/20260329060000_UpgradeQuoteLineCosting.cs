using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vectrik.Data.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class UpgradeQuoteLineCosting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OverheadCostEach",
                table: "QuoteLines");

            migrationBuilder.DropColumn(
                name: "SetupCostEach",
                table: "QuoteLines");

            migrationBuilder.DropColumn(
                name: "StackLevel",
                table: "QuoteLines");
        }
    }
}
