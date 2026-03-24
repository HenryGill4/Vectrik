using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Opcentrix_V3.Data.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddShiftManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Color",
                table: "OperatingShifts",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "OperatingShifts",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MachineShiftAssignments",
                columns: table => new
                {
                    MachineId = table.Column<int>(type: "INTEGER", nullable: false),
                    OperatingShiftId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MachineShiftAssignments", x => new { x.MachineId, x.OperatingShiftId });
                    table.ForeignKey(
                        name: "FK_MachineShiftAssignments_Machines_MachineId",
                        column: x => x.MachineId,
                        principalTable: "Machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MachineShiftAssignments_OperatingShifts_OperatingShiftId",
                        column: x => x.OperatingShiftId,
                        principalTable: "OperatingShifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserShiftAssignments",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    OperatingShiftId = table.Column<int>(type: "INTEGER", nullable: false),
                    IsPrimary = table.Column<bool>(type: "INTEGER", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AssignedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserShiftAssignments", x => new { x.UserId, x.OperatingShiftId });
                    table.ForeignKey(
                        name: "FK_UserShiftAssignments_OperatingShifts_OperatingShiftId",
                        column: x => x.OperatingShiftId,
                        principalTable: "OperatingShifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserShiftAssignments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MachineShiftAssignments_OperatingShiftId",
                table: "MachineShiftAssignments",
                column: "OperatingShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_UserShiftAssignments_OperatingShiftId",
                table: "UserShiftAssignments",
                column: "OperatingShiftId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MachineShiftAssignments");

            migrationBuilder.DropTable(
                name: "UserShiftAssignments");

            migrationBuilder.DropColumn(
                name: "Color",
                table: "OperatingShifts");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "OperatingShifts");
        }
    }
}
