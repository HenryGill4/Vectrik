using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vectrik.Data.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddQuoteLossTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LossReason",
                table: "Quotes",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "LossNotes",
                table: "Quotes",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CompetitorPrice",
                table: "Quotes",
                type: "decimal(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DecisionDays",
                table: "Quotes",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "LossReason", table: "Quotes");
            migrationBuilder.DropColumn(name: "LossNotes", table: "Quotes");
            migrationBuilder.DropColumn(name: "CompetitorPrice", table: "Quotes");
            migrationBuilder.DropColumn(name: "DecisionDays", table: "Quotes");
        }
    }
}
