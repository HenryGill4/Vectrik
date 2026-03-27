using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vectrik.Data.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddDevIssuePromptFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AcceptanceCriteria",
                table: "DevIssues",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RelatedFiles",
                table: "DevIssues",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SystemArea",
                table: "DevIssues",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcceptanceCriteria",
                table: "DevIssues");

            migrationBuilder.DropColumn(
                name: "RelatedFiles",
                table: "DevIssues");

            migrationBuilder.DropColumn(
                name: "SystemArea",
                table: "DevIssues");
        }
    }
}
