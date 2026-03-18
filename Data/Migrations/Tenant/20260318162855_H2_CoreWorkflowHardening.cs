using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Opcentrix_V3.Data.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class H2_CoreWorkflowHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeclineReason",
                table: "RfqRequests",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "JobId1",
                table: "DelayLogs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DelayLogs_JobId1",
                table: "DelayLogs",
                column: "JobId1");

            migrationBuilder.AddForeignKey(
                name: "FK_DelayLogs_Jobs_JobId1",
                table: "DelayLogs",
                column: "JobId1",
                principalTable: "Jobs",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DelayLogs_Jobs_JobId1",
                table: "DelayLogs");

            migrationBuilder.DropIndex(
                name: "IX_DelayLogs_JobId1",
                table: "DelayLogs");

            migrationBuilder.DropColumn(
                name: "DeclineReason",
                table: "RfqRequests");

            migrationBuilder.DropColumn(
                name: "JobId1",
                table: "DelayLogs");
        }
    }
}
