using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfluencerMatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCreatorTierAndQuotaFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreatorTier",
                table: "Creators",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Creators",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSmallCreator",
                table: "Creators",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Creators",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Creators_CreatorTier",
                table: "Creators",
                column: "CreatorTier");

            migrationBuilder.CreateIndex(
                name: "IX_Creators_IsSmallCreator",
                table: "Creators",
                column: "IsSmallCreator");

            migrationBuilder.CreateIndex(
                name: "IX_Creators_Subscribers",
                table: "Creators",
                column: "Subscribers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Creators_CreatorTier",
                table: "Creators");

            migrationBuilder.DropIndex(
                name: "IX_Creators_IsSmallCreator",
                table: "Creators");

            migrationBuilder.DropIndex(
                name: "IX_Creators_Subscribers",
                table: "Creators");

            migrationBuilder.DropColumn(
                name: "CreatorTier",
                table: "Creators");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Creators");

            migrationBuilder.DropColumn(
                name: "IsSmallCreator",
                table: "Creators");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Creators");
        }
    }
}
