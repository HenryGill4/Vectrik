using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Opcentrix_V3.Data.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddStageSignOff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SignOffChecklistJson",
                table: "StageExecutions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SignedOffAt",
                table: "StageExecutions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignedOffBy",
                table: "StageExecutions",
                type: "TEXT",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SignOffChecklistJson",
                table: "StageExecutions");

            migrationBuilder.DropColumn(
                name: "SignedOffAt",
                table: "StageExecutions");

            migrationBuilder.DropColumn(
                name: "SignedOffBy",
                table: "StageExecutions");
        }
    }
}
