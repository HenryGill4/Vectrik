using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Opcentrix_V3.Data.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddPhase5And6_OperatorRolesAndExternalOps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "DefaultTurnaroundDays",
                table: "ProductionStages",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsExternalOperation",
                table: "ProductionStages",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "RequiredOperatorRoleId",
                table: "ProductionStages",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ExternalOperations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StageExecutionId = table.Column<int>(type: "INTEGER", nullable: false),
                    VendorName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    VendorContact = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    PurchaseOrderNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ShipDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ExpectedReturnDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ActualReturnDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    OutboundTrackingNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ReturnTrackingNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    EstimatedTurnaroundDays = table.Column<double>(type: "REAL", nullable: true),
                    ActualTurnaroundDays = table.Column<double>(type: "REAL", nullable: true),
                    AverageTurnaroundDays = table.Column<double>(type: "REAL", nullable: true),
                    TurnaroundSampleCount = table.Column<int>(type: "INTEGER", nullable: false),
                    RequiresAtfNotification = table.Column<bool>(type: "INTEGER", nullable: false),
                    AtfShipNotificationDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AtfReceiveNotificationDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AtfShipNotified = table.Column<bool>(type: "INTEGER", nullable: false),
                    AtfReceiveNotified = table.Column<bool>(type: "INTEGER", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    ReceivedQuantity = table.Column<int>(type: "INTEGER", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalOperations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalOperations_StageExecutions_StageExecutionId",
                        column: x => x.StageExecutionId,
                        principalTable: "StageExecutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OperatorRoles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Slug = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperatorRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserOperatorRoles",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    OperatorRoleId = table.Column<int>(type: "INTEGER", nullable: false),
                    AssignedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AssignedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserOperatorRoles", x => new { x.UserId, x.OperatorRoleId });
                    table.ForeignKey(
                        name: "FK_UserOperatorRoles_OperatorRoles_OperatorRoleId",
                        column: x => x.OperatorRoleId,
                        principalTable: "OperatorRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserOperatorRoles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductionStages_RequiredOperatorRoleId",
                table: "ProductionStages",
                column: "RequiredOperatorRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalOperations_StageExecutionId",
                table: "ExternalOperations",
                column: "StageExecutionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperatorRoles_Slug",
                table: "OperatorRoles",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserOperatorRoles_OperatorRoleId",
                table: "UserOperatorRoles",
                column: "OperatorRoleId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductionStages_OperatorRoles_RequiredOperatorRoleId",
                table: "ProductionStages",
                column: "RequiredOperatorRoleId",
                principalTable: "OperatorRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductionStages_OperatorRoles_RequiredOperatorRoleId",
                table: "ProductionStages");

            migrationBuilder.DropTable(
                name: "ExternalOperations");

            migrationBuilder.DropTable(
                name: "UserOperatorRoles");

            migrationBuilder.DropTable(
                name: "OperatorRoles");

            migrationBuilder.DropIndex(
                name: "IX_ProductionStages_RequiredOperatorRoleId",
                table: "ProductionStages");

            migrationBuilder.DropColumn(
                name: "DefaultTurnaroundDays",
                table: "ProductionStages");

            migrationBuilder.DropColumn(
                name: "IsExternalOperation",
                table: "ProductionStages");

            migrationBuilder.DropColumn(
                name: "RequiredOperatorRoleId",
                table: "ProductionStages");
        }
    }
}
