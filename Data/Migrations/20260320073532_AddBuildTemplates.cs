using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vectrik.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBuildTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BuildTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    MaterialId = table.Column<int>(type: "INTEGER", nullable: true),
                    StackLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    EstimatedDurationHours = table.Column<double>(type: "REAL", nullable: false),
                    BuildParameters = table.Column<string>(type: "TEXT", nullable: true),
                    CertifiedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CertifiedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UseCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LastUsedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SourceBuildPackageId = table.Column<int>(type: "INTEGER", nullable: true),
                    PartVersionHash = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    NeedsRecertification = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BuildTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BuildTemplates_BuildPackages_SourceBuildPackageId",
                        column: x => x.SourceBuildPackageId,
                        principalTable: "BuildPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_BuildTemplates_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "BuildTemplateParts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BuildTemplateId = table.Column<int>(type: "INTEGER", nullable: false),
                    PartId = table.Column<int>(type: "INTEGER", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    StackLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    PositionNotes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BuildTemplateParts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BuildTemplateParts_BuildTemplates_BuildTemplateId",
                        column: x => x.BuildTemplateId,
                        principalTable: "BuildTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BuildTemplateParts_Parts_PartId",
                        column: x => x.PartId,
                        principalTable: "Parts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BuildTemplateParts_BuildTemplateId_PartId",
                table: "BuildTemplateParts",
                columns: new[] { "BuildTemplateId", "PartId" });

            migrationBuilder.CreateIndex(
                name: "IX_BuildTemplateParts_PartId",
                table: "BuildTemplateParts",
                column: "PartId");

            migrationBuilder.CreateIndex(
                name: "IX_BuildTemplates_MaterialId",
                table: "BuildTemplates",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_BuildTemplates_SourceBuildPackageId",
                table: "BuildTemplates",
                column: "SourceBuildPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_BuildTemplates_Status",
                table: "BuildTemplates",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BuildTemplateParts");

            migrationBuilder.DropTable(
                name: "BuildTemplates");
        }
    }
}
