using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vectrik.Data.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddSourceBuildPackageId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SourceBuildPackageId",
                table: "BuildPackages",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BuildPackages_SourceBuildPackageId",
                table: "BuildPackages",
                column: "SourceBuildPackageId");

            migrationBuilder.AddForeignKey(
                name: "FK_BuildPackages_BuildPackages_SourceBuildPackageId",
                table: "BuildPackages",
                column: "SourceBuildPackageId",
                principalTable: "BuildPackages",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BuildPackages_BuildPackages_SourceBuildPackageId",
                table: "BuildPackages");

            migrationBuilder.DropIndex(
                name: "IX_BuildPackages_SourceBuildPackageId",
                table: "BuildPackages");

            migrationBuilder.DropColumn(
                name: "SourceBuildPackageId",
                table: "BuildPackages");
        }
    }
}
