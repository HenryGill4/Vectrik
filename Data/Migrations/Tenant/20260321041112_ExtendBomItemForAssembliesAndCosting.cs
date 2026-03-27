using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vectrik.Data.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class ExtendBomItemForAssembliesAndCosting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ChildPartId",
                table: "PartBomItems",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ItemType",
                table: "PartBomItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ReferenceDesignator",
                table: "PartBomItems",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ScrapFactorPct",
                table: "PartBomItems",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitCost",
                table: "PartBomItems",
                type: "decimal(10,4)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PartBomItems_ChildPartId",
                table: "PartBomItems",
                column: "ChildPartId");

            migrationBuilder.AddForeignKey(
                name: "FK_PartBomItems_Parts_ChildPartId",
                table: "PartBomItems",
                column: "ChildPartId",
                principalTable: "Parts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PartBomItems_Parts_ChildPartId",
                table: "PartBomItems");

            migrationBuilder.DropIndex(
                name: "IX_PartBomItems_ChildPartId",
                table: "PartBomItems");

            migrationBuilder.DropColumn(
                name: "ChildPartId",
                table: "PartBomItems");

            migrationBuilder.DropColumn(
                name: "ItemType",
                table: "PartBomItems");

            migrationBuilder.DropColumn(
                name: "ReferenceDesignator",
                table: "PartBomItems");

            migrationBuilder.DropColumn(
                name: "ScrapFactorPct",
                table: "PartBomItems");

            migrationBuilder.DropColumn(
                name: "UnitCost",
                table: "PartBomItems");
        }
    }
}
