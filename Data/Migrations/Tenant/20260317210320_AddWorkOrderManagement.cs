using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Opcentrix_V3.Data.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddWorkOrderManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ActualShipDate",
                table: "WorkOrders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovedBy",
                table: "WorkOrders",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedDate",
                table: "WorkOrders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContractLineItem",
                table: "WorkOrders",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContractNumber",
                table: "WorkOrders",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomFieldValues",
                table: "WorkOrders",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsDefenseContract",
                table: "WorkOrders",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "PromisedDate",
                table: "WorkOrders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ShipByDate",
                table: "WorkOrders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WorkflowInstanceId",
                table: "WorkOrders",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WorkOrderComments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    WorkOrderId = table.Column<int>(type: "INTEGER", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    AuthorName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    AuthorUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    ParentCommentId = table.Column<int>(type: "INTEGER", nullable: true),
                    IsInternal = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EditedDate = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrderComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkOrderComments_Users_AuthorUserId",
                        column: x => x.AuthorUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WorkOrderComments_WorkOrderComments_ParentCommentId",
                        column: x => x.ParentCommentId,
                        principalTable: "WorkOrderComments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WorkOrderComments_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_WorkflowInstanceId",
                table: "WorkOrders",
                column: "WorkflowInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderComments_AuthorUserId",
                table: "WorkOrderComments",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderComments_ParentCommentId",
                table: "WorkOrderComments",
                column: "ParentCommentId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderComments_WorkOrderId",
                table: "WorkOrderComments",
                column: "WorkOrderId");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkOrders_WorkflowInstances_WorkflowInstanceId",
                table: "WorkOrders",
                column: "WorkflowInstanceId",
                principalTable: "WorkflowInstances",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkOrders_WorkflowInstances_WorkflowInstanceId",
                table: "WorkOrders");

            migrationBuilder.DropTable(
                name: "WorkOrderComments");

            migrationBuilder.DropIndex(
                name: "IX_WorkOrders_WorkflowInstanceId",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "ActualShipDate",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "ApprovedBy",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "ApprovedDate",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "ContractLineItem",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "ContractNumber",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "CustomFieldValues",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "IsDefenseContract",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "PromisedDate",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "ShipByDate",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "WorkflowInstanceId",
                table: "WorkOrders");
        }
    }
}
