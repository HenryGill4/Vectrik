using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vectrik.Data.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class BuildSystemRedesign_TemplateSlicerFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "BuildHeightMm",
                table: "BuildTemplates",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "EstimatedPowderKg",
                table: "BuildTemplates",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileName",
                table: "BuildTemplates",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LayerCount",
                table: "BuildTemplates",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PartPositionsJson",
                table: "BuildTemplates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SlicerSoftware",
                table: "BuildTemplates",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SlicerVersion",
                table: "BuildTemplates",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BuildTemplateId",
                table: "BuildPackages",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BuildTemplateRevisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BuildTemplateId = table.Column<int>(type: "INTEGER", nullable: false),
                    RevisionNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    RevisionDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ChangedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ChangeNotes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    PartsSnapshotJson = table.Column<string>(type: "TEXT", nullable: false),
                    ParametersSnapshotJson = table.Column<string>(type: "TEXT", nullable: true),
                    SlicerMetadataSnapshotJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BuildTemplateRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BuildTemplateRevisions_BuildTemplates_BuildTemplateId",
                        column: x => x.BuildTemplateId,
                        principalTable: "BuildTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BuildPackages_BuildTemplateId",
                table: "BuildPackages",
                column: "BuildTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_BuildTemplateRevisions_BuildTemplateId",
                table: "BuildTemplateRevisions",
                column: "BuildTemplateId");

            migrationBuilder.AddForeignKey(
                name: "FK_BuildPackages_BuildTemplates_BuildTemplateId",
                table: "BuildPackages",
                column: "BuildTemplateId",
                principalTable: "BuildTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Data migration: copy slicer metadata from BuildFileInfos to the BuildTemplate
            // that was created from the same BuildPackage (via SourceBuildPackageId).
            migrationBuilder.Sql("""
                UPDATE BuildTemplates
                SET FileName           = (SELECT bfi.FileName           FROM BuildFileInfos bfi INNER JOIN BuildPackages bp ON bp.Id = bfi.BuildPackageId WHERE bp.Id = BuildTemplates.SourceBuildPackageId LIMIT 1),
                    LayerCount         = (SELECT bfi.LayerCount         FROM BuildFileInfos bfi INNER JOIN BuildPackages bp ON bp.Id = bfi.BuildPackageId WHERE bp.Id = BuildTemplates.SourceBuildPackageId LIMIT 1),
                    BuildHeightMm      = (SELECT bfi.BuildHeightMm      FROM BuildFileInfos bfi INNER JOIN BuildPackages bp ON bp.Id = bfi.BuildPackageId WHERE bp.Id = BuildTemplates.SourceBuildPackageId LIMIT 1),
                    EstimatedPowderKg  = (SELECT bfi.EstimatedPowderKg  FROM BuildFileInfos bfi INNER JOIN BuildPackages bp ON bp.Id = bfi.BuildPackageId WHERE bp.Id = BuildTemplates.SourceBuildPackageId LIMIT 1),
                    PartPositionsJson  = (SELECT bfi.PartPositionsJson  FROM BuildFileInfos bfi INNER JOIN BuildPackages bp ON bp.Id = bfi.BuildPackageId WHERE bp.Id = BuildTemplates.SourceBuildPackageId LIMIT 1),
                    SlicerSoftware     = (SELECT bfi.SlicerSoftware     FROM BuildFileInfos bfi INNER JOIN BuildPackages bp ON bp.Id = bfi.BuildPackageId WHERE bp.Id = BuildTemplates.SourceBuildPackageId LIMIT 1),
                    SlicerVersion      = (SELECT bfi.SlicerVersion      FROM BuildFileInfos bfi INNER JOIN BuildPackages bp ON bp.Id = bfi.BuildPackageId WHERE bp.Id = BuildTemplates.SourceBuildPackageId LIMIT 1)
                WHERE BuildTemplates.SourceBuildPackageId IS NOT NULL;
                """);

            // Data migration: migrate Draft/Sliced BuildPackages to Ready status (enum value 2).
            // Draft=0, Sliced=1, Ready=2
            migrationBuilder.Sql("""
                UPDATE BuildPackages SET Status = 2 WHERE Status IN (0, 1);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BuildPackages_BuildTemplates_BuildTemplateId",
                table: "BuildPackages");

            migrationBuilder.DropTable(
                name: "BuildTemplateRevisions");

            migrationBuilder.DropIndex(
                name: "IX_BuildPackages_BuildTemplateId",
                table: "BuildPackages");

            migrationBuilder.DropColumn(
                name: "BuildHeightMm",
                table: "BuildTemplates");

            migrationBuilder.DropColumn(
                name: "EstimatedPowderKg",
                table: "BuildTemplates");

            migrationBuilder.DropColumn(
                name: "FileName",
                table: "BuildTemplates");

            migrationBuilder.DropColumn(
                name: "LayerCount",
                table: "BuildTemplates");

            migrationBuilder.DropColumn(
                name: "PartPositionsJson",
                table: "BuildTemplates");

            migrationBuilder.DropColumn(
                name: "SlicerSoftware",
                table: "BuildTemplates");

            migrationBuilder.DropColumn(
                name: "SlicerVersion",
                table: "BuildTemplates");

            migrationBuilder.DropColumn(
                name: "BuildTemplateId",
                table: "BuildPackages");
        }
    }
}
