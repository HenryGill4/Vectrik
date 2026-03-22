using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Opcentrix_V3.Data.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddMachineIsAdditiveMachine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAdditiveMachine",
                table: "Machines",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            // Seed existing SLS/Additive machines as additive
            migrationBuilder.Sql(
                "UPDATE Machines SET IsAdditiveMachine = 1 WHERE LOWER(MachineType) IN ('sls', 'additive');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsAdditiveMachine",
                table: "Machines");
        }
    }
}
