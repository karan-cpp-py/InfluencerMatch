using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfluencerMatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentWebhookTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProviderEventId",
                table: "PaymentRecords",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderRawPayload",
                table: "PaymentRecords",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "PaymentRecords",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRecords_ProviderEventId",
                table: "PaymentRecords",
                column: "ProviderEventId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PaymentRecords_ProviderEventId",
                table: "PaymentRecords");

            migrationBuilder.DropColumn(
                name: "ProviderEventId",
                table: "PaymentRecords");

            migrationBuilder.DropColumn(
                name: "ProviderRawPayload",
                table: "PaymentRecords");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "PaymentRecords");
        }
    }
}
