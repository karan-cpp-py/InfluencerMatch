using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfluencerMatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEnterpriseLeadOwnerStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OwnerUserId",
                table: "EnterpriseLeads",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "EnterpriseLeads",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "EnterpriseLeads",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EnterpriseLeads_OwnerUserId",
                table: "EnterpriseLeads",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EnterpriseLeads_Status",
                table: "EnterpriseLeads",
                column: "Status");

            migrationBuilder.AddForeignKey(
                name: "FK_EnterpriseLeads_Users_OwnerUserId",
                table: "EnterpriseLeads",
                column: "OwnerUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EnterpriseLeads_Users_OwnerUserId",
                table: "EnterpriseLeads");

            migrationBuilder.DropIndex(
                name: "IX_EnterpriseLeads_OwnerUserId",
                table: "EnterpriseLeads");

            migrationBuilder.DropIndex(
                name: "IX_EnterpriseLeads_Status",
                table: "EnterpriseLeads");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "EnterpriseLeads");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "EnterpriseLeads");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "EnterpriseLeads");
        }
    }
}
