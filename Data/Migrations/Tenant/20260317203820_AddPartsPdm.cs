using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Opcentrix_V3.Data.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddPartsPdm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomFieldValues",
                table: "Parts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerPartNumber",
                table: "Parts",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DrawingNumber",
                table: "Parts",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "EstimatedWeightKg",
                table: "Parts",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDefensePart",
                table: "Parts",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ItarClassification",
                table: "Parts",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RawMaterialSpec",
                table: "Parts",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Revision",
                table: "Parts",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RevisionDate",
                table: "Parts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PartDrawings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PartId = table.Column<int>(type: "INTEGER", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    FileType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Revision = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    IsPrimary = table.Column<bool>(type: "INTEGER", nullable: false),
                    UploadedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UploadedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartDrawings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartDrawings_Parts_PartId",
                        column: x => x.PartId,
                        principalTable: "Parts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PartNotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PartId = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    NoteType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    IsPinned = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartNotes_Parts_PartId",
                        column: x => x.PartId,
                        principalTable: "Parts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PartRevisionHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PartId = table.Column<int>(type: "INTEGER", nullable: false),
                    Revision = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    PreviousRevision = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    ChangeDescription = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    RawMaterialSpec = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    DrawingNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    RoutingSnapshot = table.Column<string>(type: "TEXT", nullable: true),
                    RevisionDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartRevisionHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartRevisionHistories_Parts_PartId",
                        column: x => x.PartId,
                        principalTable: "Parts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PartDrawings_PartId",
                table: "PartDrawings",
                column: "PartId");

            migrationBuilder.CreateIndex(
                name: "IX_PartNotes_PartId",
                table: "PartNotes",
                column: "PartId");

            migrationBuilder.CreateIndex(
                name: "IX_PartRevisionHistories_PartId",
                table: "PartRevisionHistories",
                column: "PartId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PartDrawings");

            migrationBuilder.DropTable(
                name: "PartNotes");

            migrationBuilder.DropTable(
                name: "PartRevisionHistories");

            migrationBuilder.DropColumn(
                name: "CustomFieldValues",
                table: "Parts");

            migrationBuilder.DropColumn(
                name: "CustomerPartNumber",
                table: "Parts");

            migrationBuilder.DropColumn(
                name: "DrawingNumber",
                table: "Parts");

            migrationBuilder.DropColumn(
                name: "EstimatedWeightKg",
                table: "Parts");

            migrationBuilder.DropColumn(
                name: "IsDefensePart",
                table: "Parts");

            migrationBuilder.DropColumn(
                name: "ItarClassification",
                table: "Parts");

            migrationBuilder.DropColumn(
                name: "RawMaterialSpec",
                table: "Parts");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "Parts");

            migrationBuilder.DropColumn(
                name: "RevisionDate",
                table: "Parts");
        }
    }
}
