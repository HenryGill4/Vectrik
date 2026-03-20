using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Opcentrix_V3.Data.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddDeferredSerialization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PartInstances_SerialNumber",
                table: "PartInstances");

            migrationBuilder.AlterColumn<string>(
                name: "SerialNumber",
                table: "PartInstances",
                type: "TEXT",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<bool>(
                name: "IsSerialAssigned",
                table: "PartInstances",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TemporaryTrackingId",
                table: "PartInstances",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_PartInstances_SerialNumber",
                table: "PartInstances",
                column: "SerialNumber",
                unique: true,
                filter: "\"SerialNumber\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PartInstances_TemporaryTrackingId",
                table: "PartInstances",
                column: "TemporaryTrackingId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PartInstances_SerialNumber",
                table: "PartInstances");

            migrationBuilder.DropIndex(
                name: "IX_PartInstances_TemporaryTrackingId",
                table: "PartInstances");

            migrationBuilder.DropColumn(
                name: "IsSerialAssigned",
                table: "PartInstances");

            migrationBuilder.DropColumn(
                name: "TemporaryTrackingId",
                table: "PartInstances");

            migrationBuilder.AlterColumn<string>(
                name: "SerialNumber",
                table: "PartInstances",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PartInstances_SerialNumber",
                table: "PartInstances",
                column: "SerialNumber",
                unique: true);
        }
    }
}
