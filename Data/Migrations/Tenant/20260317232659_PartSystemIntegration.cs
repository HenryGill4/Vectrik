using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vectrik.Data.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class PartSystemIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaterialId",
                table: "Parts",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PartBomItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PartId = table.Column<int>(type: "INTEGER", nullable: false),
                    InventoryItemId = table.Column<int>(type: "INTEGER", nullable: true),
                    MaterialId = table.Column<int>(type: "INTEGER", nullable: true),
                    QuantityRequired = table.Column<decimal>(type: "decimal(10,4)", nullable: false),
                    UnitOfMeasure = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartBomItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartBomItems_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PartBomItems_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PartBomItems_Parts_PartId",
                        column: x => x.PartId,
                        principalTable: "Parts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Parts_MaterialId",
                table: "Parts",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_PartBomItems_InventoryItemId",
                table: "PartBomItems",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PartBomItems_MaterialId",
                table: "PartBomItems",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_PartBomItems_PartId",
                table: "PartBomItems",
                column: "PartId");

            migrationBuilder.AddForeignKey(
                name: "FK_Parts_Materials_MaterialId",
                table: "Parts",
                column: "MaterialId",
                principalTable: "Materials",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Parts_Materials_MaterialId",
                table: "Parts");

            migrationBuilder.DropTable(
                name: "PartBomItems");

            migrationBuilder.DropIndex(
                name: "IX_Parts_MaterialId",
                table: "Parts");

            migrationBuilder.DropColumn(
                name: "MaterialId",
                table: "Parts");
        }
    }
}
