using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Opcentrix_V3.Data.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddManufacturingApproach : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ManufacturingApproach",
                table: "Parts");

            migrationBuilder.AddColumn<int>(
                name: "ManufacturingApproachId",
                table: "Parts",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ManufacturingApproaches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Slug = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IconEmoji = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    IsAdditive = table.Column<bool>(type: "INTEGER", nullable: false),
                    RequiresBuildPlate = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasPostPrintBatching = table.Column<bool>(type: "INTEGER", nullable: false),
                    DefaultRoutingTemplate = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManufacturingApproaches", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Parts_ManufacturingApproachId",
                table: "Parts",
                column: "ManufacturingApproachId");

            migrationBuilder.CreateIndex(
                name: "IX_ManufacturingApproaches_Slug",
                table: "ManufacturingApproaches",
                column: "Slug",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Parts_ManufacturingApproaches_ManufacturingApproachId",
                table: "Parts",
                column: "ManufacturingApproachId",
                principalTable: "ManufacturingApproaches",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Parts_ManufacturingApproaches_ManufacturingApproachId",
                table: "Parts");

            migrationBuilder.DropTable(
                name: "ManufacturingApproaches");

            migrationBuilder.DropIndex(
                name: "IX_Parts_ManufacturingApproachId",
                table: "Parts");

            migrationBuilder.DropColumn(
                name: "ManufacturingApproachId",
                table: "Parts");

            migrationBuilder.AddColumn<string>(
                name: "ManufacturingApproach",
                table: "Parts",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "");
        }
    }
}
