using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Opcentrix_V3.Data.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddPartInstanceBuildPackageId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BuildPackageId",
                table: "PartInstances",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PartInstances_BuildPackageId",
                table: "PartInstances",
                column: "BuildPackageId");

            migrationBuilder.AddForeignKey(
                name: "FK_PartInstances_BuildPackages_BuildPackageId",
                table: "PartInstances",
                column: "BuildPackageId",
                principalTable: "BuildPackages",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PartInstances_BuildPackages_BuildPackageId",
                table: "PartInstances");

            migrationBuilder.DropIndex(
                name: "IX_PartInstances_BuildPackageId",
                table: "PartInstances");

            migrationBuilder.DropColumn(
                name: "BuildPackageId",
                table: "PartInstances");
        }
    }
}
