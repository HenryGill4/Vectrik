using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vectrik.Data.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddCertifiedLayouts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CertifiedLayoutId",
                table: "ProgramParts",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlateSlots",
                table: "ProgramParts",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "UsesCertifiedLayouts",
                table: "MachinePrograms",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PlateCompositionJson",
                table: "BuildTemplates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CertifiedLayouts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Size = table.Column<int>(type: "INTEGER", nullable: false),
                    PartId = table.Column<int>(type: "INTEGER", nullable: false),
                    Positions = table.Column<int>(type: "INTEGER", nullable: false),
                    StackLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    MaterialId = table.Column<int>(type: "INTEGER", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CertifiedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CertifiedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PartVersionHash = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    NeedsRecertification = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    UseCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LastUsedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CertifiedLayouts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CertifiedLayouts_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CertifiedLayouts_Parts_PartId",
                        column: x => x.PartId,
                        principalTable: "Parts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CertifiedLayoutRevisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CertifiedLayoutId = table.Column<int>(type: "INTEGER", nullable: false),
                    RevisionNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    RevisionDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ChangedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ChangeNotes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    PreviousPartId = table.Column<int>(type: "INTEGER", nullable: false),
                    PreviousPositions = table.Column<int>(type: "INTEGER", nullable: false),
                    PreviousStackLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    PreviousNotes = table.Column<string>(type: "TEXT", nullable: true),
                    SnapshotJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CertifiedLayoutRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CertifiedLayoutRevisions_CertifiedLayouts_CertifiedLayoutId",
                        column: x => x.CertifiedLayoutId,
                        principalTable: "CertifiedLayouts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProgramParts_CertifiedLayoutId",
                table: "ProgramParts",
                column: "CertifiedLayoutId");

            migrationBuilder.CreateIndex(
                name: "IX_CertifiedLayoutRevisions_CertifiedLayoutId",
                table: "CertifiedLayoutRevisions",
                column: "CertifiedLayoutId");

            migrationBuilder.CreateIndex(
                name: "IX_CertifiedLayouts_MaterialId",
                table: "CertifiedLayouts",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_CertifiedLayouts_PartId",
                table: "CertifiedLayouts",
                column: "PartId");

            migrationBuilder.CreateIndex(
                name: "IX_CertifiedLayouts_Status",
                table: "CertifiedLayouts",
                column: "Status");

            migrationBuilder.AddForeignKey(
                name: "FK_ProgramParts_CertifiedLayouts_CertifiedLayoutId",
                table: "ProgramParts",
                column: "CertifiedLayoutId",
                principalTable: "CertifiedLayouts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProgramParts_CertifiedLayouts_CertifiedLayoutId",
                table: "ProgramParts");

            migrationBuilder.DropTable(
                name: "CertifiedLayoutRevisions");

            migrationBuilder.DropTable(
                name: "CertifiedLayouts");

            migrationBuilder.DropIndex(
                name: "IX_ProgramParts_CertifiedLayoutId",
                table: "ProgramParts");

            migrationBuilder.DropColumn(
                name: "CertifiedLayoutId",
                table: "ProgramParts");

            migrationBuilder.DropColumn(
                name: "PlateSlots",
                table: "ProgramParts");

            migrationBuilder.DropColumn(
                name: "UsesCertifiedLayouts",
                table: "MachinePrograms");

            migrationBuilder.DropColumn(
                name: "PlateCompositionJson",
                table: "BuildTemplates");
        }
    }
}
