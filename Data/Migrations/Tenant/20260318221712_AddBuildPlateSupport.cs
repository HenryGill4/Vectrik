using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vectrik.Data.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddBuildPlateSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BuildPackageId",
                table: "StageExecutions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsBuildLevelStage",
                table: "ProductionStages",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CustomFieldValues",
                table: "NonConformanceReports",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JobNumber",
                table: "Jobs",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomFieldValues",
                table: "InventoryItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BuildParameters",
                table: "BuildPackages",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrentRevision",
                table: "BuildPackages",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BuildPackageRevisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BuildPackageId = table.Column<int>(type: "INTEGER", nullable: false),
                    RevisionNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    RevisionDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ChangedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ChangeNotes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    PartsSnapshotJson = table.Column<string>(type: "TEXT", nullable: false),
                    ParametersSnapshotJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BuildPackageRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BuildPackageRevisions_BuildPackages_BuildPackageId",
                        column: x => x.BuildPackageId,
                        principalTable: "BuildPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StageExecutions_BuildPackageId",
                table: "StageExecutions",
                column: "BuildPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_BuildPackageRevisions_BuildPackageId",
                table: "BuildPackageRevisions",
                column: "BuildPackageId");

            migrationBuilder.AddForeignKey(
                name: "FK_StageExecutions_BuildPackages_BuildPackageId",
                table: "StageExecutions",
                column: "BuildPackageId",
                principalTable: "BuildPackages",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StageExecutions_BuildPackages_BuildPackageId",
                table: "StageExecutions");

            migrationBuilder.DropTable(
                name: "BuildPackageRevisions");

            migrationBuilder.DropIndex(
                name: "IX_StageExecutions_BuildPackageId",
                table: "StageExecutions");

            migrationBuilder.DropColumn(
                name: "BuildPackageId",
                table: "StageExecutions");

            migrationBuilder.DropColumn(
                name: "IsBuildLevelStage",
                table: "ProductionStages");

            migrationBuilder.DropColumn(
                name: "CustomFieldValues",
                table: "NonConformanceReports");

            migrationBuilder.DropColumn(
                name: "JobNumber",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "CustomFieldValues",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "BuildParameters",
                table: "BuildPackages");

            migrationBuilder.DropColumn(
                name: "CurrentRevision",
                table: "BuildPackages");
        }
    }
}
