using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Opcentrix_V3.Data.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddQuoting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AcceptedAt",
                table: "Quotes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContractNumber",
                table: "Quotes",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Quotes",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomFieldValues",
                table: "Quotes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedLaborCost",
                table: "Quotes",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedMaterialCost",
                table: "Quotes",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedOverheadCost",
                table: "Quotes",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "IsDefenseContract",
                table: "Quotes",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                table: "Quotes",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastModifiedDate",
                table: "Quotes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RevisionNumber",
                table: "Quotes",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "SentAt",
                table: "Quotes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TargetMarginPct",
                table: "Quotes",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<double>(
                name: "LaborMinutes",
                table: "QuoteLines",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<decimal>(
                name: "MaterialCostEach",
                table: "QuoteLines",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OutsideProcessCost",
                table: "QuoteLines",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<double>(
                name: "SetupMinutes",
                table: "QuoteLines",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.CreateTable(
                name: "QuoteRevisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    QuoteId = table.Column<int>(type: "INTEGER", nullable: false),
                    RevisionNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalEstimatedCost = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    QuotedPrice = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    EstimatedLaborCost = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    EstimatedMaterialCost = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    EstimatedOverheadCost = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    TargetMarginPct = table.Column<decimal>(type: "TEXT", nullable: false),
                    LinesSnapshot = table.Column<string>(type: "TEXT", nullable: true),
                    ChangeNotes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuoteRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuoteRevisions_Quotes_QuoteId",
                        column: x => x.QuoteId,
                        principalTable: "Quotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RfqRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CompanyName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ContactName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: true),
                    Material = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    NeededByDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AttachmentPaths = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ConvertedQuoteId = table.Column<int>(type: "INTEGER", nullable: true),
                    SubmittedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ReviewedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ReviewedDate = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RfqRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RfqRequests_Quotes_ConvertedQuoteId",
                        column: x => x.ConvertedQuoteId,
                        principalTable: "Quotes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuoteRevisions_QuoteId",
                table: "QuoteRevisions",
                column: "QuoteId");

            migrationBuilder.CreateIndex(
                name: "IX_RfqRequests_ConvertedQuoteId",
                table: "RfqRequests",
                column: "ConvertedQuoteId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuoteRevisions");

            migrationBuilder.DropTable(
                name: "RfqRequests");

            migrationBuilder.DropColumn(
                name: "AcceptedAt",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "ContractNumber",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "CustomFieldValues",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "EstimatedLaborCost",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "EstimatedMaterialCost",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "EstimatedOverheadCost",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "IsDefenseContract",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "LastModifiedDate",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "RevisionNumber",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "SentAt",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "TargetMarginPct",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "LaborMinutes",
                table: "QuoteLines");

            migrationBuilder.DropColumn(
                name: "MaterialCostEach",
                table: "QuoteLines");

            migrationBuilder.DropColumn(
                name: "OutsideProcessCost",
                table: "QuoteLines");

            migrationBuilder.DropColumn(
                name: "SetupMinutes",
                table: "QuoteLines");
        }
    }
}
